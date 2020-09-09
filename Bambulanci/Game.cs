using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms.VisualStyles;

namespace Bambulanci
{
	public enum WeaponType { Pistol, Shotgun, Machinegun }
	
	public interface ICollectableObject
	{
		public int Id { get; }
		public float X { get; }
		public float Y { get; }
		public int SizePx { get; }
		const int Noone = -1;
		public int CollectedBy { get; set; }
		public WeaponType WeaponContained { get; }
	}

	class WeaponBox : ICollectableObject
	{
		public int Id { get; }
		public float X { get; }
		public float Y { get; }
		public int SizePx { get; }
		public int CollectedBy { get; set; } = ICollectableObject.Noone;
		public WeaponType WeaponContained { get; }
		private WeaponBox(int id, float x, float y, FormBambulanci form, WeaponType weaponType)
		{
			Id = id;
			X = x;
			Y = y;
			WeaponContained = weaponType;
			SizePx = form.Game.graphicsDrawer.BoxSizePx;
		}
		public static WeaponBox Generate(int id, float x, float y, FormBambulanci form, WeaponType weaponType)
		{
			WeaponBox newBox = null;
			switch (weaponType)
			{
				case WeaponType.Pistol:
					newBox = new WeaponBox(id, x, y, form, WeaponType.Pistol);
					break;
				case WeaponType.Shotgun:
					newBox = new WeaponBox(id, x, y, form, WeaponType.Shotgun);
					break;
				case WeaponType.Machinegun:
					newBox = new WeaponBox(id, x, y, form, WeaponType.Machinegun);
					break;
				default:
					break;
			}
			form.Game.boxIdCounter++;
			return newBox;
		}
	}

	public enum Direction { Left, Up, Right, Down, Stay }
	public enum WeaponState { Fired, Still}

	public abstract class Weapon
	{
		protected readonly FormBambulanci form;
		protected readonly Player player;

		public abstract int Cooldown { get; }
		protected int currentCooldown;
		public Weapon(FormBambulanci form, Player player)
		{
			this.form = form;
			this.player = player;
		}
		public virtual void Fire(WeaponState weaponState)
		{
			if (weaponState == WeaponState.Fired && currentCooldown <= 0)
			{
				float offset = form.Game.graphicsDrawer.PlayerSizePx / 2f / form.Width;
				lock (form.Game.Projectiles)
				{
					form.Game.Projectiles.Add(new Projectile(player.X + offset, player.Y + offset, player.Direction, player.projectileIdGenerator, form, player.PlayerId));
				}
				player.projectileIdGenerator++;
				currentCooldown = Cooldown;
			}
			else
			{
				currentCooldown--;
			}
		}
	}
	sealed class Pistol : Weapon
	{
		public override int Cooldown => 1; //30
		public Pistol(FormBambulanci form, Player player) : base(form, player) { }
	}

	sealed class Shotgun : Weapon
	{
		public override int Cooldown => 50;
		public Shotgun(FormBambulanci form, Player player) : base(form, player) { }

		const float shellOffset = 20f; //might throw error on map without walls
		public override void Fire(WeaponState weaponState)
		{
			if (weaponState == WeaponState.Fired && currentCooldown <= 0)
			{

				float offset = form.Game.graphicsDrawer.PlayerSizePx / 2f / form.Width;
				float projectileMidX = player.X + offset;
				float projectileMidY = player.Y + offset;

				float offsetX = 0;
				float offsetY = 0;
				if (player.Direction == Direction.Up || player.Direction == Direction.Down)
				{
					offsetX = shellOffset / form.Width;
				}
				if (player.Direction == Direction.Left || player.Direction == Direction.Right)
				{
					offsetY = shellOffset / form.TrueHeight;
				}

				lock (form.Game.Projectiles)
				{
					form.Game.Projectiles.Add(new Projectile(projectileMidX - offsetX, projectileMidY - offsetY, player.Direction, player.projectileIdGenerator, form, player.PlayerId));
					player.projectileIdGenerator++;

					form.Game.Projectiles.Add(new Projectile(projectileMidX, projectileMidY, player.Direction, player.projectileIdGenerator, form, player.PlayerId));
					player.projectileIdGenerator++;

					form.Game.Projectiles.Add(new Projectile(projectileMidX + offsetX, projectileMidY + offsetY, player.Direction, player.projectileIdGenerator, form, player.PlayerId));
					player.projectileIdGenerator++;
				}
				currentCooldown = Cooldown;
			}
			else
			{
				currentCooldown--;
			}
		}
	}
	sealed class Machinegun : Weapon
	{
		public override int Cooldown => 5;
		public Machinegun(FormBambulanci form, Player player) : base(form, player) { }
	}

	public interface IMovableObject
	{
		public float X { get; set; }
		public float Y { get; set; }

		public Direction Direction { get; }
		public float Speed { get; }
		public int PlayerId { get; }
		public int SizePx { get; }

	}
	public class Projectile : IMovableObject
	{
		//between 0 and 1
		public float X { get; set; }
		public float Y { get; set; }

		public Direction Direction { get; set; }
		public float Speed { get; } = 0.02f; //const but from Iface
		public int PlayerId { get; }
		public int SizePx { get; }

		public bool shouldBeDestroyed = false; //host only

		public readonly int id;

		public Projectile(float x, float y, Direction direction, int id, FormBambulanci form, int playerId = -1)
		{
			this.X = x;
			this.Y = y;
			this.Direction = direction;
			this.PlayerId = playerId;
			this.id = id;
			SizePx = form.Game.graphicsDrawer.ProjectileSizePx;
		}

	}


	public class Player : IMovableObject
	{
		public int PlayerId { get; }

		//toDo:
		public int kills = 0;
		public int deaths = 0;

		public bool isAlive = true;
		public int killedBy = -1;
		public int respawnTimer = 0;

		//constants for player image scaling
		//public const float widthScaling = 32;
		//public const float heightScaling = 18;

		public int SizePx { get; }

		//coords between 0 and 1
		public float X { get; set; }
		public float Y { get; set; }
		public float Speed { get; } = 0.01f;

		public Direction Direction { get; private set; } //definitely not Stay

		public Weapon Weapon { get; private set; }
		const int projectileIdMultiplier = 1000000;
		public int projectileIdGenerator;
		public readonly IPEndPoint ipEndPoint; //for host only

		private readonly FormBambulanci form;

		public Player(FormBambulanci form, float x, float y, int id, Direction direction = Direction.Left, IPEndPoint ipEndPoint = null)
		{
			this.X = x;
			this.Y = y;
			this.PlayerId = id;
			this.Direction = direction;
			this.ipEndPoint = ipEndPoint;
			this.form = form;
			ChangeWeapon(WeaponType.Pistol);
			projectileIdGenerator = projectileIdMultiplier * id;
			SizePx = form.Game.graphicsDrawer.PlayerSizePx;
		}


		/// <summary>
		/// Called by host only.
		/// </summary>
		/// <param name="playerSize"> in pixels </param>
		public void MoveByHost(Direction direction, FormBambulanci form) //form just for graphics debuging //graphicsDrawer for playerSize -- not absolutely necessary, window collision is bad anyways
		{ //might be moved under ref directly??----------
			if (direction != Direction.Stay)
			{
				Direction = direction;
				form.Game.Move(this);
				//form.Game.Move(Direction, ref X, ref Y, speed, graphicsDrawer.PlayerWidthPx, graphicsDrawer.PlayerHeightPx, Id);
			}
		}
		public void MoveByClient(Direction direction, float x, float y)
		{
			this.Direction = direction;
			this.X = x;
			this.Y = y;
		}
		public void ChangeWeapon(WeaponType weaponType)
		{
			switch (weaponType)
			{
				case WeaponType.Pistol:
					Weapon = new Pistol(form, this);
					break;
				case WeaponType.Shotgun:
					Weapon = new Shotgun(form, this);
					break;
				case WeaponType.Machinegun:
					Weapon = new Machinegun(form, this);
					break;
				default:
					break;
			}
		}
	}

	public class Map
	{
		public readonly int cols;
		public readonly int rows;
		public readonly Size tileSizeScaled;

		private Bitmap[] tiles;
		private int[,] grid;
		private int[] wallTiles;

		private Map(int cols, int rows, Size tileSizeScaled)
		{
			this.cols = cols;
			this.rows = rows;
			this.tileSizeScaled = tileSizeScaled;
		}

		public static Map GetStandardMap(int formWidth, int formHeight)
		{
			int cols = 20;
			int rows = 12;
			Size tileSizeScaled = new Size(formWidth / cols, formHeight / rows);
			Map result = new Map(cols,rows, tileSizeScaled);

			Bitmap tileAtlas = Properties.Resources.standardTileAtlas;
			int atlasCols = 6;
			int tileCount = 24;
			Size tileSize = new Size(48, 48);

			//tileCache
			result.tiles = new Bitmap[tileCount];
			for (int i = 0; i < tileCount; i ++)
			{
				int x = i % atlasCols * tileSize.Width;
				int y = i / atlasCols * tileSize.Height;

				Rectangle cloneRect = new Rectangle(x, y, tileSize.Width, tileSize.Height);
				Bitmap tile = tileAtlas.Clone(cloneRect, tileAtlas.PixelFormat); //pixelFormat??
				Bitmap tileScaled = GraphicsDrawer.ResizeImage(tile, result.tileSizeScaled.Width, result.tileSizeScaled.Height);
				
				result.tiles[i] = tileScaled;	
			}

			result.grid = new int[,]
			{
				{ 7,18,18,18,18,18,18,18,18,18,18,18,18,18,18,18,18,18,18,6 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,15,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,15,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,15,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 9,20,20,20,20,20,20,20,20,20,20,20,20,20,20,20,20,20,20,8 },
			};

			result.wallTiles = new int[] { 6, 7, 8, 9, 18, 19, 20, 21, 15 };
			return result;
		}

		public Bitmap GetTile(int column, int row)
		{
			int tileNum = grid[row, column];
			return tiles[tileNum];
		}

		public bool IsWall(int col, int row) //might throw index out of range error
		{
			return Array.IndexOf(wallTiles, grid[row, col]) != -1;
		}
	}

	public class GraphicsDrawer
	{
		private readonly int formWidth;
		private readonly int formHeight;
		private readonly Map map;

		private readonly Bitmap[] playerImg; //left, up, right, down
		private readonly Brush[] allowedColors = new Brush[] { Brushes.Yellow, Brushes.Red, Brushes.Aqua, Brushes.BlueViolet, Brushes.Chocolate };

		private readonly Bitmap projectileImg;
		private readonly Bitmap boxImg;
		public GraphicsDrawer(int formWidth, int formHeight, Map map)
		{
			this.formWidth = formWidth;
			this.formHeight = formHeight;
			this.map = map;
			playerImg = CreatePlayerImg();
			projectileImg = CreateProjectileImg();
			boxImg = CreateBoxImg();
		}

		const int colorsPerPlayer = 4;


		//in pixels:
		public int PlayerSizePx { get; private set; }
		public int ProjectileSizePx { get; private set; }
		public int BoxSizePx { get; private set; }

		/// <summary>
		/// Draws background from map to Graphics g
		/// </summary>
		public void DrawBackground(Graphics g)
		{
			for (int column = 0; column < map.cols; column++)
				for (int row = 0; row < map.rows; row++)
				{
					Bitmap tile = map.GetTile(column, row);
					g.DrawImage(tile, column * map.tileSizeScaled.Width, row * map.tileSizeScaled.Height);
				}
		}


		/// <summary>
		/// Creaters array of playerImg based on allowedColors.
		/// Player desing of each color occurs 4 times, always rotated by 90 degrees.
		/// In order: Left, Up, Right, Down
		/// </summary>
		private Bitmap[] CreatePlayerImg()
		{
			const int eyeScaling = 3;

			Bitmap[] result = new Bitmap[allowedColors.Length * colorsPerPlayer];
			for (int i = 0; i < allowedColors.Length; i++)
			{
				Brush playerColor = allowedColors[i];
				PlayerSizePx = (int)(formWidth / 32f);

				Bitmap bitmap = new Bitmap(PlayerSizePx, PlayerSizePx);
				var g = Graphics.FromImage(bitmap);

				int eyeSizePx = (int)((float)PlayerSizePx / eyeScaling);
				int offset = (PlayerSizePx / 2 - eyeSizePx) / 2;
				g.FillRectangle(playerColor, new Rectangle(0, 0, PlayerSizePx, PlayerSizePx));
				g.FillEllipse(Brushes.Black, new Rectangle(0, offset, eyeSizePx, eyeSizePx));
				g.FillEllipse(Brushes.Black, new Rectangle(0, offset + PlayerSizePx / 2, eyeSizePx, eyeSizePx));

				Bitmap b90 = (Bitmap)bitmap.Clone();
				b90.RotateFlip(RotateFlipType.Rotate90FlipNone);

				Bitmap b180 = (Bitmap)bitmap.Clone();
				b180.RotateFlip(RotateFlipType.Rotate180FlipNone);

				Bitmap b270 = (Bitmap)bitmap.Clone();
				b270.RotateFlip(RotateFlipType.Rotate270FlipNone);

				result[4 * i] = bitmap;
				result[4 * i + 1] = b90;
				result[4 * i + 2] = b180;
				result[4 * i + 3] = b270;
			}
			return result;
		}

		public void DrawPlayer(Graphics g, Player player)
		{
			int i = player.PlayerId * colorsPerPlayer + (byte)player.Direction;
			int mod = allowedColors.Length * colorsPerPlayer;
			Bitmap playerBitmap = playerImg[i % mod];
			g.DrawImage(playerBitmap, player.X * formWidth, player.Y * formHeight);

			//g.DrawRectangle(Pens.Silver, player.X*formWidth, player.Y*formHeight, 100, 100); //player hitbox
		}

		private Bitmap CreateProjectileImg()
		{
			//ProjectileWidthPx = ProjectileWiPx
			ProjectileSizePx = formWidth / 128;
			
			Bitmap bitmap = new Bitmap(ProjectileSizePx, ProjectileSizePx);
			var g = Graphics.FromImage(bitmap);
			g.FillRectangle(Brushes.Orange, 0, 0, ProjectileSizePx, ProjectileSizePx);

			return bitmap;
		}

		public void DrawProjectile(Graphics g, Projectile projectile)
		{
			g.DrawImage(projectileImg, projectile.X * formWidth, projectile.Y * formHeight);
		}

		private Bitmap CreateBoxImg()
		{
			BoxSizePx = formWidth / 42;
			
			Bitmap bitmap = new Bitmap(BoxSizePx, BoxSizePx);
			var g = Graphics.FromImage(bitmap);
			g.FillRectangle(Brushes.Brown, 0, 0, BoxSizePx, BoxSizePx);
			
			return bitmap;
		}
		public void DrawBox(Graphics g, ICollectableObject box)
		{
			g.DrawImage(boxImg, box.X * formWidth, box.Y * formHeight);
		}


		//taken from: https://stackoverflow.com/questions/1922040/how-to-resize-an-image-c-sharp
		/// <summary>
		/// Resize the image to the specified width and height.
		/// </summary>
		/// <param name="image">The image to resize.</param>
		/// <param name="width">The width to resize to.</param>
		/// <param name="height">The height to resize to.</param>
		/// <returns>The resized image.</returns>
		public static Bitmap ResizeImage(Image image, int width, int height)
		{
			var destRect = new Rectangle(0, 0, width, height);
			var destImage = new Bitmap(width, height);

			using (var graphics = Graphics.FromImage(destImage))
			{
				graphics.CompositingMode = CompositingMode.SourceCopy;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

				using (var wrapMode = new ImageAttributes())
				{
					wrapMode.SetWrapMode(WrapMode.TileFlipXY);
					graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
				}
			}

			return destImage;
		}

	}

	public class Game
	{
		public readonly GraphicsDrawer graphicsDrawer;
		public readonly Map map;
		private readonly int formWidth;
		private readonly int formHeight;


		private const int notFound = -1;
		private const int respawnTime = 100;

		public List<Player> Players { get; set; } = new List<Player>();
		public List<Player> DeadPlayers { get; set; } = new List<Player>();
		public List<Projectile> Projectiles { get; private set; } = new List<Projectile>();
		public int boxIdCounter = 1;
		public List<ICollectableObject> Boxes { get;  set; } = new List<ICollectableObject>();

		public Game(int formWidth, int formHeight)
		{
			this.formHeight = formHeight;
			this.formWidth = formWidth;
			map = Map.GetStandardMap(formWidth, formHeight); //might be delegate in case of multiple maps
			graphicsDrawer = new GraphicsDrawer(formWidth, formHeight, map);
		}

		/// <summary>
		/// Return spawn coords on tile that is not a wall.
		/// </summary>
		/// <returns> Coords scaled between 0 and 1. </returns>
		public (float, float) GetSpawnCoords(Random rng)
		{
			int col = rng.Next(map.cols);
			int row = rng.Next(map.rows);

			while (map.IsWall(col, row))
			{
				col = rng.Next(map.cols);
				row = rng.Next(map.rows);

			}
			return ((float)col * map.tileSizeScaled.Width / formWidth, (float)row * map.tileSizeScaled.Height / formHeight);
		}

		private bool Overlaps(float newX, float newY, float objSizePx, float obstacleXPx, float obstacleYPx, float obstacleWidthPx, float obstacleHeightPx)
		{
			//newX, newY between 0 and 1
			float objL = newX * formWidth;
			float objR = newX * formWidth + objSizePx;
			float objT = newY * formHeight;
			float objB = newY * formHeight + objSizePx;

			//obstacleX,obstacleY in pixels
			float obstacleL = obstacleXPx;
			float obstacleR = obstacleXPx + obstacleWidthPx;
			float obstacleT = obstacleYPx;
			float obstacleB = obstacleYPx + obstacleHeightPx;
			
			bool xOverlap = objL < obstacleR && objR > obstacleL;
			bool yOverlap = objT < obstacleB && objB > obstacleT;

			return xOverlap && yOverlap;
		}

		private (bool collided, int objXPx, int objYPx) DetectWalls(float newX, float newY, IMovableObject obj)
		{
			//https://jonathanwhiting.com/tutorial/collision/			
			//Graphics g = formular.CreateGraphics();
			int tileW = map.tileSizeScaled.Width;
			int tileH = map.tileSizeScaled.Height;

			//get current tile
			int tileCol = (int)(newX * formWidth / tileW);
			int tileRow = (int)(newY * formHeight / tileH);

			int tileColMax = (int)((newX * formWidth + obj.SizePx) / tileW);
			int tileRowMax = (int)((newY * formHeight + obj.SizePx) / tileH);
			for (int col = tileCol; col <= tileColMax; col++)
				for (int row = tileRow; row <= tileRowMax; row++)
				{
					if (map.IsWall(col, row))
					{
						if (Overlaps(newX, newY, obj.SizePx, col * tileW, row * tileH, tileW, tileH))
						{
							return (true, col * map.tileSizeScaled.Width, row * map.tileSizeScaled.Height);
						}
					}
				}
			return (false, 0, 0);
		}

		private (bool collided, int xPx, int yPx, int playerId) DetectPlayers(float newX, float newY, IMovableObject obj)
		{
			lock (Players)
			{
				foreach (var player in Players)
				{
					if (player.PlayerId != obj.PlayerId)
					{
						if (Overlaps(newX, newY, obj.SizePx, player.X*formWidth, player.Y*formHeight, player.SizePx, player.SizePx))
						{
							return (true, (int)(player.X * formWidth), (int)(player.Y * formHeight), player.PlayerId);
						}
					}
				}
			}
			return (false, 0, 0, -1);
		}

		private (bool collided, int boxId) DetectBoxes(float newX, float newY, IMovableObject obj)
		{
			lock (Boxes)
			{
				foreach (var box in Boxes)
				{
					if (Overlaps(newX, newY, obj.SizePx, box.X * formWidth, box.Y * formHeight, box.SizePx, box.SizePx))
					{
						return (true, box.Id);
					}
				}
			}
			return (false, 0);
		}

		/// <summary>
		/// Moves player towards collided wall.
		/// </summary>
		private void CollisionResponseHug(int objXPx, int objYPx, int objWidthPx, int objHeightPx, float X, float Y, ref float newX, ref float newY, int SizePx)
		{
			float horizontal = X - newX;
			float vertical = Y - newY;

			if(horizontal < 0) //-right
			{
				newX = (float)(objXPx - SizePx - 1) / formWidth;
			}
			if(horizontal > 0) //+left
			{
				newX = (float)(objXPx + objWidthPx + 1) / formWidth;
			}
			if(vertical > 0) //+up
			{
				newY = (float)(objYPx + objHeightPx + 1) / formHeight;
			}
			if (vertical < 0) //-down
			{
				newY = (float)(objYPx - SizePx - 1) / formHeight;
			}

		}

		public void Move(IMovableObject obj, int projectileId = -1) //projectileId in case of projectile only
		{
			float newX = obj.X;
			float newY = obj.Y;
			switch (obj.Direction)
			{
				case Direction.Left:
					newX -= obj.Speed;
					break;
				case Direction.Right:
					newX += obj.Speed;
					break;
				case Direction.Up:
					newY -= obj.Speed;
					break;
				case Direction.Down:
					newY += obj.Speed;
					break;
				default:
					break;
			}

			(bool playerCollision, int playerXPx, int playerYPx, int playerId) = DetectPlayers(newX, newY, obj);// obj.X, obj.Y, obj.SizePx, obj.PlayerId);
			(bool wallCollision, int wallXPx, int wallYPx) = DetectWalls(newX, newY, obj);// obj.X, obj.Y, obj.SizePx);
			
			bool boxCollision = false;
			int boxId = 0;
			if (projectileId == -1) //player only
			{
				(boxCollision, boxId) = DetectBoxes(newX, newY, obj);
			}
			
			bool windowCollision = newX < 0 || newX > 1 - (float)obj.SizePx / formWidth || newY < 0 || newY > 1 - (float)obj.SizePx / formHeight;

			if (playerCollision)
			{
				CollisionResponseHug(playerXPx, playerYPx, graphicsDrawer.PlayerSizePx, graphicsDrawer.PlayerSizePx, obj.X, obj.Y, ref newX, ref newY, obj.SizePx);
				if(projectileId != -1)
				{
					MarkProjectileForDestruction(projectileId);
					lock (Players)
					{
						int index = Players.FindIndex(p => p.PlayerId == playerId);
						if (index != notFound)
						{
							Players[index].isAlive = false;
							Players[index].killedBy = obj.PlayerId;
							Players[index].respawnTimer = respawnTime;
						}
					}
				}
			}
			if (wallCollision)
			{
				int wallWidth = map.tileSizeScaled.Width;
				int wallHeight = map.tileSizeScaled.Height;
				CollisionResponseHug(wallXPx, wallYPx, wallWidth, wallHeight, obj.X, obj.Y, ref newX, ref newY, obj.SizePx);
				if (projectileId != -1)
				{
					MarkProjectileForDestruction(projectileId);
				}
			}
			if (boxCollision)
			{
				lock (Boxes)
				{
					int index = Boxes.FindIndex(b => b.Id == boxId);
					if (index != notFound)
					{
						Boxes[index].CollectedBy = obj.PlayerId;
					}
				}
			}
			if (!windowCollision)
			{
				obj.X = newX;
				obj.Y = newY;
			}
			else if (projectileId != -1)
			{
				MarkProjectileForDestruction(projectileId);
			}
		}
		private void MarkProjectileForDestruction(int projectileId)
		{
			lock (Projectiles)
			{
				int index = Projectiles.FindIndex(p => p.id == projectileId);
				if (index != notFound)
				{
					Projectiles[index].shouldBeDestroyed = true;
				}
			}
		}
	}
}
