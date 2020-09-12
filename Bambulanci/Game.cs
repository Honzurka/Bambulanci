using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace Bambulanci
{
	public enum WeaponType { Pistol, Shotgun, Machinegun }

	public interface IDrawable
	{
		public float X { get; }
		public float Y { get; }
		public int GetSizePx();
	}

	public interface ICollectableObject : IDrawable
	{
		public int Id { get; }
		const int Noone = -1;
		public int CollectedBy { get; set; }
		public WeaponType WeaponContained { get; }
	}

	class WeaponBox : ICollectableObject
	{
		public int Id { get; }
		public float X { get; }
		public float Y { get; }
		public static int SizePx { get; private set; }
		public static void SetSize(int formWidth)
		{
			SizePx = formWidth / 42;

		}
		public int GetSizePx() => SizePx;

		public int CollectedBy { get; set; } = ICollectableObject.Noone;
		public WeaponType WeaponContained { get; }
		private WeaponBox(int id, float x, float y, WeaponType weaponType)
		{
			Id = id;
			X = x;
			Y = y;
			WeaponContained = weaponType;
		}
		public static WeaponBox Generate(Data data)
		{
			(int boxId, byte b, float x, float y) = data.Values;
			WeaponType weaponType = (WeaponType)b;
			WeaponBox newBox = null;
			switch (weaponType)
			{
				case WeaponType.Pistol:
					newBox = new WeaponBox(boxId, x, y, WeaponType.Pistol);
					break;
				case WeaponType.Shotgun:
					newBox = new WeaponBox(boxId, x, y, WeaponType.Shotgun);
					break;
				case WeaponType.Machinegun:
					newBox = new WeaponBox(boxId, x, y, WeaponType.Machinegun);
					break;
				default:
					break;
			}
			return newBox;
		}
	}

	public enum Direction { Left, Up, Right, Down, Stay }
	public enum WeaponState { Fired, Still}

	public abstract class Weapon
	{
		protected readonly Player player;
		protected readonly Game game;

		public abstract int Cooldown { get; }
		protected int currentCooldown;
		public Weapon(Game game, Player player)
		{
			this.game = game;
			this.player = player;
		}
		public virtual void Fire()
		{
			if (currentCooldown <= 0)
			{
				float offset = Player.SizePx / 2f / FormBambulanci.WidthStatic;
				lock (game.Projectiles)
				{
					game.Projectiles.Add(new Projectile(game, player.X + offset, player.Y + offset, player.Direction, player.projectileIdGenerator, player.PlayerId));
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
		public override int Cooldown => 30;
		public Pistol(Game game, Player player) : base(game, player) { }
	}

	sealed class Shotgun : Weapon
	{
		public override int Cooldown => 50;
		public Shotgun(Game game, Player player) : base(game, player) { }

		const float shellOffset = 20f;
		public override void Fire()
		{
			if (currentCooldown <= 0)
			{

				float offset = Player.SizePx / 2f / FormBambulanci.WidthStatic;
				float projectileMidX = player.X + offset;
				float projectileMidY = player.Y + offset;

				float offsetX = 0;
				float offsetY = 0;
				if (player.Direction == Direction.Up || player.Direction == Direction.Down)
				{
					offsetX = shellOffset / FormBambulanci.WidthStatic;
				}
				if (player.Direction == Direction.Left || player.Direction == Direction.Right)
				{
					offsetY = shellOffset / FormBambulanci.HeightStatic;
				}

				lock (game.Projectiles)
				{
					game.Projectiles.Add(new Projectile(game, projectileMidX - offsetX, projectileMidY - offsetY, player.Direction, player.projectileIdGenerator, player.PlayerId));
					player.projectileIdGenerator++;

					game.Projectiles.Add(new Projectile(game, projectileMidX, projectileMidY, player.Direction, player.projectileIdGenerator,  player.PlayerId));
					player.projectileIdGenerator++;

					game.Projectiles.Add(new Projectile(game, projectileMidX + offsetX, projectileMidY + offsetY, player.Direction, player.projectileIdGenerator, player.PlayerId));
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
		public Machinegun(Game game, Player player) : base(game, player) { }
	}

	public abstract class MovableObject : IDrawable
	{
		protected const int notUsed = -1;
		protected const int notFound = -1;

		public float X { get; set; }
		public float Y { get; set; }
		public abstract int GetSizePx();
		public Direction Direction { get; set; } //player's direction had private set....
		public abstract float GetSpeed();
		protected readonly Game game;
		
		public int PlayerId { get; set; }

		protected virtual bool CanCollectBoxes { get; set; } = false;
		protected virtual void DestroyIfDestructible() { }

		protected MovableObject(Game game) => this.game = game;

		private void CalculateNewCoords(out float newX, out float newY)
		{
			newX = X;
			newY = Y;
			switch (Direction)
			{
				case Direction.Left:
					newX -= GetSpeed();
					break;
				case Direction.Right:
					newX += GetSpeed();
					break;
				case Direction.Up:
					newY -= GetSpeed();
					break;
				case Direction.Down:
					newY += GetSpeed();
					break;
				default:
					break;
			}
		}
		public void Move()
		{
			CalculateNewCoords(out float newX, out float newY);
			bool wallDetected = DetectWalls(ref newX, ref newY);
			bool playerDetected = DetectAndKillPlayer(ref newX, ref newY); //check player.Die properties
			
			if(CanCollectBoxes)
				CollectBoxes(newX, newY);

			if (wallDetected || playerDetected)
			{
				DestroyIfDestructible();
			}

			X = newX;
			Y = newY;
		}

		private bool Overlaps(float newX, float newY, float obstacleXPx, float obstacleYPx, float obstacleWidthPx, float obstacleHeightPx)
		{
			int formWidth = FormBambulanci.WidthStatic;
			int formHeight = FormBambulanci.HeightStatic;

			//newX, newY between 0 and 1
			float objL = newX * formWidth;
			float objR = newX * formWidth + GetSizePx();
			float objT = newY * formHeight;
			float objB = newY * formHeight + GetSizePx();

			//obstacleX,obstacleY in pixels
			float obstacleL = obstacleXPx;
			float obstacleR = obstacleXPx + obstacleWidthPx;
			float obstacleT = obstacleYPx;
			float obstacleB = obstacleYPx + obstacleHeightPx;

			bool xOverlap = objL < obstacleR && objR > obstacleL;
			bool yOverlap = objT < obstacleB && objB > obstacleT;

			return xOverlap && yOverlap;
		}

		private bool DetectWalls(ref float newX, ref float newY)
		{
			int tileWidth = game.map.tileSizeScaled.Width;
			int tileHeight = game.map.tileSizeScaled.Height;
			int formWidth = FormBambulanci.WidthStatic;
			int formHeight = FormBambulanci.HeightStatic;

			//get top-left tile
			int tileCol = (int)(newX * formWidth / tileWidth);
			int tileRow = (int)(newY * formHeight / tileHeight);

			//get bottom-right tile
			int tileColMax = (int)((newX * formWidth + GetSizePx()) / tileWidth);
			int tileRowMax = (int)((newY * formHeight + GetSizePx()) / tileHeight);

			for (int col = tileCol; col <= tileColMax; col++)
				for (int row = tileRow; row <= tileRowMax; row++)
				{
					if (game.map.IsWall(col, row))
					{
						int wallXPx = col * tileWidth;
						int wallYPx = row * tileHeight;
						if (Overlaps(newX, newY, wallXPx, wallYPx, tileWidth, tileHeight)) 
						{
							HugObject(ref newX, ref newY, wallXPx, wallYPx, tileWidth, tileHeight);
							return true;
						}
					}
				}
			return false;
		}

		private bool DetectAndKillPlayer(ref float newX, ref float newY)
		{
			int formWidth = FormBambulanci.WidthStatic;
			int formHeight = FormBambulanci.HeightStatic;

			lock (game.Players)
			{
				foreach (var player in game.Players)
				{
					if (player.PlayerId != PlayerId)
					{
						int playerXPx = (int) (player.X * formWidth);
						int playerYPx = (int) (player.Y * formHeight);
						if (Overlaps(newX, newY, playerXPx, playerYPx, Player.SizePx, Player.SizePx))
						{
							HugObject(ref newX, ref newY, playerXPx, playerYPx, Player.SizePx, Player.SizePx);
							player.Die(PlayerId);
							return true;
						}
					}
				}
			}
			return false;
		}

		private void CollectBoxes(float newX, float newY)
		{
			lock (game.Boxes)
			{
				foreach (var box in game.Boxes)
				{
					int boxXPx = (int)(box.X * FormBambulanci.WidthStatic);
					int boxYPx = (int)(box.Y * FormBambulanci.HeightStatic);
					if (Overlaps(newX, newY, boxXPx, boxYPx, box.GetSizePx(), box.GetSizePx()))
					{
						box.CollectedBy = PlayerId;
					}
				}
			}
		}

		/// <summary>
		/// Moves player towards collided object.
		/// </summary>
		private void HugObject(ref float newX, ref float newY, int objXPx, int objYPx, int objWidthPx, int objHeightPx)
		{
			int formWidth = FormBambulanci.WidthStatic;
			int formHeight = FormBambulanci.HeightStatic;

			float horizontal = X - newX;
			float vertical = Y - newY;

			if (horizontal < 0) //-right
			{
				newX = (float)(objXPx - GetSizePx() - 1) / formWidth;
			}
			else if (horizontal > 0) //+left
			{
				newX = (float)(objXPx + objWidthPx + 1) / formWidth;
			}
			else if (vertical > 0) //+up
			{
				newY = (float)(objYPx + objHeightPx + 1) / formHeight;
			}
			else //-down
			{
				newY = (float)(objYPx - GetSizePx() - 1) / formHeight;
			}
		}

	}
	public class Projectile : MovableObject
	{
		public bool shouldBeDestroyed = false; //host only
		public readonly int id; //host only

		public override float GetSpeed() => 0.02f;
		public static int SizePx { get; private set; }
		public static void SetSize(int formWidth)
		{
			SizePx = formWidth / 128;
		}
		public override int GetSizePx() => SizePx;

		public Projectile(Game game, float x, float y, Direction direction, int id, int playerId = notUsed) : base(game)
		{
			this.X = x;
			this.Y = y;
			this.Direction = direction;
			this.PlayerId = playerId;
			this.id = id;
		}
		protected override void DestroyIfDestructible()
		{
			lock (game.Projectiles)
			{
				int index = game.Projectiles.FindIndex(p => p.id == id);
				if (index != notFound)
				{
					game.Projectiles[index].shouldBeDestroyed = true;
				}
			}
		}
	}

	public class Player : MovableObject
	{
		private const int respawnTime = 100;

		public int Kills { get; set; } = 0;
		public int Deaths { get; set; } = 0;

		public bool IsAlive { get; set; } = true;
		public int KilledBy { get; private set; } = -1;
		public int RespawnTimer { get; set; } = 0;

		public static int SizePx { get; private set; }
		public static void SetSize(int formWidth)
		{
			SizePx = formWidth / 32; //(int)(formWidth / 32f)
		}
		public override int GetSizePx() => SizePx;
		public override float GetSpeed() => 0.01f;
		protected override bool CanCollectBoxes { get; set; } = true;
		private Weapon weapon;
		const int projectileIdMultiplier = 1000000;
		public int projectileIdGenerator;
		public readonly IPEndPoint ipEndPoint; //host only

		public Player(Game game, float x, float y, int id, Direction direction = Direction.Left, IPEndPoint ipEndPoint = null) : base(game)
		{
			this.X = x;
			this.Y = y;
			this.PlayerId = id;
			this.Direction = direction;
			this.ipEndPoint = ipEndPoint;
			ChangeWeapon(WeaponType.Pistol);
			projectileIdGenerator = projectileIdMultiplier * id;
		}

		public void MoveByClient(Direction direction, float x, float y)
		{
			this.Direction = direction;
			this.X = x;
			this.Y = y;
		}
		public void MoveByHost(Direction playerMovement)
		{
			if(playerMovement != Direction.Stay)
			{
				Direction = playerMovement;
				Move();
			}
		}

		public void FireWeapon(WeaponState weaponState)
		{
			if (weaponState != WeaponState.Still)
				weapon.Fire();
		}

		public void ChangeWeapon(WeaponType weaponType)
		{
			switch (weaponType)
			{
				case WeaponType.Pistol:
					weapon = new Pistol(game, this);
					break;
				case WeaponType.Shotgun:
					weapon = new Shotgun(game, this);
					break;
				case WeaponType.Machinegun:
					weapon = new Machinegun(game, this);
					break;
				default:
					break;
			}
		}
		
		public void Die(int killedById)
		{
			IsAlive = false;
			KilledBy = killedById;
			RespawnTimer = respawnTime;
		}
	}

	public class Map
	{
		private const int notFound = -1; //information duplicity, notFound is always -1...

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

		public static Map GetStandardMap()
		{
			int cols = 20;
			int rows = 12;
			Size tileSizeScaled = new Size(FormBambulanci.WidthStatic / cols, FormBambulanci.HeightStatic / rows);
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

		public bool IsWall(int col, int row)
		{
			if (col < 0 || col >= cols || row < 0 || row >= rows)
			{
				return true;
			}
			else
			{
				return Array.IndexOf(wallTiles, grid[row, col]) != notFound;
			}
		}
	}

	public class GraphicsDrawer
	{
		private readonly int formWidth = FormBambulanci.WidthStatic;
		private readonly int formHeight = FormBambulanci.HeightStatic;
		private readonly Map map;

		const int imagesPerColor = 4; //1 for each direction
		private readonly Bitmap[] playerImg; // [left, up, right, down] of each color
		private readonly Brush[] allowedColors = new Brush[] { Brushes.Yellow, Brushes.Red, Brushes.Aqua, Brushes.BlueViolet };

		private readonly Bitmap projectileImg;
		private readonly Bitmap boxImg;
		public GraphicsDrawer(Map map)
		{
			this.map = map;
			playerImg = CreatePlayerImg();
			projectileImg = CreateProjectileImg();
			boxImg = CreateBoxImg();
		}

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
			int playerSizePx = Player.SizePx;

			Bitmap[] result = new Bitmap[allowedColors.Length * imagesPerColor];
			for (int i = 0; i < allowedColors.Length; i++)
			{
				Brush playerColor = allowedColors[i];
				
				Bitmap bitmap = new Bitmap(playerSizePx, playerSizePx);
				var g = Graphics.FromImage(bitmap);

				int eyeSizePx = (int)((float)playerSizePx / eyeScaling);
				int offset = (playerSizePx / 2 - eyeSizePx) / 2;
				g.FillRectangle(playerColor, new Rectangle(0, 0, playerSizePx, playerSizePx));
				g.FillEllipse(Brushes.Black, new Rectangle(0, offset, eyeSizePx, eyeSizePx));
				g.FillEllipse(Brushes.Black, new Rectangle(0, offset + playerSizePx / 2, eyeSizePx, eyeSizePx));

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

		public void DrawPlayer(Graphics g, IDrawable drawablePlayer)
		{
			Player player = (Player)drawablePlayer;
			int i = player.PlayerId * imagesPerColor + (byte)player.Direction;
			int mod = allowedColors.Length * imagesPerColor;
			Bitmap playerBitmap = playerImg[i % mod];
			g.DrawImage(playerBitmap, player.X * formWidth, player.Y * formHeight);
		}

		private Bitmap CreateProjectileImg()
		{
			int projectileSizePx = Projectile.SizePx;
			
			Bitmap bitmap = new Bitmap(projectileSizePx, projectileSizePx);
			var g = Graphics.FromImage(bitmap);
			g.FillRectangle(Brushes.Orange, 0, 0, projectileSizePx, projectileSizePx);

			return bitmap;
		}

		public void DrawProjectile(Graphics g, IDrawable projectile)
		{
			g.DrawImage(projectileImg, projectile.X * formWidth, projectile.Y * formHeight);
		}

		private Bitmap CreateBoxImg()
		{
			Bitmap box = Properties.Resources.box;
			return ResizeImage(box, WeaponBox.SizePx, WeaponBox.SizePx);
		}
		public void DrawBox(Graphics g, IDrawable box)
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

		public List<Player> Players { get; set; } = new List<Player>();
		public List<Player> DeadPlayers { get; set; } = new List<Player>();
		public List<Projectile> Projectiles { get; private set; } = new List<Projectile>();
		public int BoxIdCounter { get; set; } = 1;
		public List<ICollectableObject> Boxes { get;  set; } = new List<ICollectableObject>();

		public Game()
		{
			formWidth = FormBambulanci.WidthStatic;
			formHeight = FormBambulanci.HeightStatic;
			map = Map.GetStandardMap(); //might be delegate in case of multiple maps
			graphicsDrawer = new GraphicsDrawer(map);
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
	}
}
