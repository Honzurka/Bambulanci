using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Xaml.Permissions;

namespace Bambulanci
{
	class Utility
	{
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
	
	public enum PlayerMovement { Left, Up, Right, Down, Stay }
	public enum WeaponState { Fired, Still}

	public interface Weapon
	{
		public void Fire(WeaponState weaponState);
	}
	class Pistol : Weapon
	{
		bool coolingDown = false;
		private readonly Game game;
		private readonly Player player;
		private int idCounter = 0;

		public Pistol(Game game, Player player)
		{
			this.game = game;
			this.player = player;
		}

		public void Fire(WeaponState weaponState)
		{
			if (weaponState == WeaponState.Fired && coolingDown == false)
			{
				int projectileId = player.id * 100000 + idCounter; //..-----------idea
				idCounter++;
				game.projectiles.Add(new Projectile(player.X, player.Y, player.Direction, projectileId)); //style of shooting--melo by vznikat ve stredu hrace. ne nahore vlevo
				coolingDown = true;
				Console.WriteLine($"#5 projectile added to list from host at dir:{player.Direction} x:{player.X} y:{player.Y} ");
			}
		}
	}
	public class Projectile
	{
		//should be between 0 and 1
		public float X;
		public float Y;

		public readonly PlayerMovement direction;
		const float speed = 0.01f; //...
		public readonly int Id;

		public Projectile(float x, float y, PlayerMovement direction, int Id)
		{
			this.X = x;
			this.Y = y;
			this.direction = direction;
			this.Id = Id;
		}

		public void MoveByHost() //similar to playerMovement -- should be 1 method only
		{
			Console.WriteLine($"projectile moved by host from x:{X} y:{Y}");
			switch (direction)
			{
				case PlayerMovement.Left:
					X -= speed;
					break;
				case PlayerMovement.Up:
					Y -= speed;
					break;
				case PlayerMovement.Right:
					X += speed;
					break;
				case PlayerMovement.Down:
					Y += speed;
					break;
				default:
					break;
			}
			Console.WriteLine($"projectile moved by host to x:{X} y:{Y}");
		}
	}


	public class Player
	{
		public readonly int id; //duplicity - also in ClientInfo
		//bool isAlive;

		public const float widthScaling = 32;
		public const float heightScaling = 18;
		
		//coords between 0 and 1
		public float X { get; private set; }
		public float Y { get; private set; }

		private const float speed = 0.01f;
		public PlayerMovement Direction { get; private set; } //definitely not Stay

		public Weapon Weapon { get; private set; }
		
		public readonly IPEndPoint ipEndPoint; //for host only

		private readonly Game game;

		public Player(Game game, float x, float y, int id = -1, PlayerMovement direction = PlayerMovement.Left, IPEndPoint ipEndPoint = null)
		{
			this.X = x;
			this.Y = y;
			this.id = id;
			this.Direction = direction;
			this.ipEndPoint = ipEndPoint;
			this.game = game;
			Weapon = new Pistol(game, this);
		}


		/// <summary>
		/// Called by host only.
		/// </summary>
		/// <param name="playerSize"> in pixels </param>
		public void MoveByHost(PlayerMovement playerMovement, GraphicsDrawer graphicsDrawer, FormBambulanci form)
			//form just for graphics debuging
			//graphicsDrawer for playerSize -- not absolutely necessary, window collision is bad anyways
		{
			if (playerMovement != PlayerMovement.Stay)
				Direction = playerMovement;

			float newX = X;
			float newY = Y;

			switch (playerMovement)
			{
				case PlayerMovement.Left:
					newX -= speed;
					break;
				case PlayerMovement.Right:
					newX += speed;
					break;
				case PlayerMovement.Up:
					newY -= speed;
					break;
				case PlayerMovement.Down:
					newY += speed;
					break;
				default:
					break;
			}
			
			//player collision might be implemented---------------------------------

			form.Game.DetectWalls(ref newX, ref newY, X, Y, form);

			if (newX >= 0 && newX <= 1 - graphicsDrawer.PlayerWidth/form.Width && newY >= 0 && newY <= 1 - graphicsDrawer.PlayerHeight/form.TrueHeight) //not perfect
			{
				X = newX;
				Y = newY;
			}
		}
		public void MoveByClient(PlayerMovement direction, float x, float y)
		{
			this.Direction = direction;
			this.X = x;
			this.Y = y;
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
				Bitmap tileScaled = Utility.ResizeImage(tile, result.tileSizeScaled.Width, result.tileSizeScaled.Height);
				
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

		private Bitmap projectileImg;
		public GraphicsDrawer(int formWidth, int formHeight, Map map)
		{
			this.formWidth = formWidth;
			this.formHeight = formHeight;
			this.map = map;
			playeImg = CreatePlayerImg();
			projectileImg = CreateProjectileImg();
		}

		const int colorsPerPlayer = 4;


		//in pixels:
		public int PlayerWidth { get; private set; } //prob. should be under player.....
		public int PlayerHeight { get; private set; }

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


		private readonly Bitmap[] playeImg; //left, up, right, down
		private readonly Brush[] allowedColors = new Brush[] { Brushes.Yellow, Brushes.Red, Brushes.Aqua, Brushes.BlueViolet, Brushes.Chocolate };
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
				PlayerWidth = (int)(formWidth/ Player.widthScaling);
				PlayerHeight = (int)(formHeight / Player.heightScaling);

				Bitmap bitmap = new Bitmap(PlayerWidth, PlayerHeight);
				var g = Graphics.FromImage(bitmap);

				int w = PlayerWidth / eyeScaling;
				int h = PlayerHeight / eyeScaling;

				int offset = (PlayerWidth / 2 - w) / 2;
				g.FillRectangle(playerColor, new Rectangle(0, 0, PlayerWidth, PlayerHeight));
				g.FillEllipse(Brushes.Black, new Rectangle(0, offset, w, h));
				g.FillEllipse(Brushes.Black, new Rectangle(0, offset + PlayerHeight / 2, w, h));

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

		private Bitmap CreateProjectileImg()
		{
			int projectileWidth = formWidth / 128; //constants...-----------
			int projectileHeight = formHeight / 72;

			Bitmap bitmap = new Bitmap(projectileWidth, projectileHeight);
			var g = Graphics.FromImage(bitmap);
			g.FillRectangle(Brushes.Orange, 0, 0, projectileWidth, projectileHeight);
			
			return bitmap;
		}

		public void DrawPlayer(Graphics g, Player player)
		{
			int i = player.id * colorsPerPlayer + (byte)player.Direction;
			int mod = allowedColors.Length * colorsPerPlayer;
			Bitmap playerBitmap = playeImg[i % mod];
			g.DrawImage(playerBitmap, player.X * formWidth, player.Y * formHeight);

			//g.DrawRectangle(Pens.Silver, player.X*formWidth, player.Y*formHeight, 100, 100); //player hitbox
		}

		public void DrawProjectile(Graphics g, Projectile projectile)
		{
			g.DrawImage(projectileImg, projectile.X*formWidth, projectile.Y*formHeight);
			Console.WriteLine($"#8 client: projectile drawn at: x:{projectile.X} y:{projectile.Y}");
		}
	}

	public class Game
		//list of players might be under game----------------------
	{
		public readonly GraphicsDrawer graphicsDrawer;
		public readonly Map map;
		private int formWidth;
		private int formHeight;

		public List<Player> Players { get; private set; } = new List<Player>();
		public List<Projectile> projectiles { get; private set; } = new List<Projectile>();

		public Game(int formWidth, int formHeight)
		{
			this.formHeight = formHeight;
			this.formWidth = formWidth;
			map = Map.GetStandardMap(formWidth, formHeight); //might be delegate in case of multiple maps
			graphicsDrawer = new GraphicsDrawer(formWidth, formHeight, map);
		}

		Random rng = new Random();
		/// <summary>
		/// Return spawn coords on tile that is not a wall.
		/// </summary>
		/// <returns> Coords scaled between 0 and 1. </returns>
		public (float, float) GetSpawnCoords()
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

		public void DetectWalls(ref float newX, ref float newY, float X, float Y, Form formular) //form just for graphic debug
		{
			//https://jonathanwhiting.com/tutorial/collision/			
			Graphics g = formular.CreateGraphics();
			int tileW = map.tileSizeScaled.Width;
			int tileH = map.tileSizeScaled.Height;

			//get current tile
			int tileCol = (int)(newX * formWidth / tileW);
			int tileRow = (int)(newY * formHeight / tileH);

			int tileColMax = (int)((newX * formWidth + graphicsDrawer.PlayerWidth) / tileW);
			int tileRowMax = (int)((newY * formHeight + graphicsDrawer.PlayerHeight) / tileH);
			for (int col = tileCol; col <= tileColMax; col++)
				for (int row = tileRow; row <= tileRowMax; row++)
				{
					if (map.IsWall(col, row))
					{
						g.DrawRectangle(Pens.Red, col * tileW, row * tileH, tileW, tileH);

						bool xOverlap = newX * formWidth < ((col + 1) * tileW - 1) && ((newX * formWidth + graphicsDrawer.PlayerWidth)) > (col * tileW);
						bool yOverlap = newY * formHeight < ((row + 1) * tileH - 1) && ((newY * formHeight + graphicsDrawer.PlayerHeight)) > (row * tileH);

						if (xOverlap && yOverlap)
						{
							CollisionResponse(col, row, X, Y, ref newX, ref newY);
							return;
						}
					}
				}
		}

		/// <summary>
		/// Moves player towards collided wall.
		/// </summary>
		private void CollisionResponse(int col, int row, float X, float Y, ref float newX, ref float newY)
		{
			float horizontal = X - newX;
			float vertical = Y - newY;

			int tileX = col * map.tileSizeScaled.Width;
			int tileY = row * map.tileSizeScaled.Height;

			if(horizontal < 0) //-right
			{
				newX = (float)(tileX - graphicsDrawer.PlayerWidth - 1) / formWidth;
			}
			if(horizontal > 0) //+left
			{
				newX = (float)(tileX + map.tileSizeScaled.Width) / formWidth;
			}
			if(vertical > 0) //+up
			{
				newY = (float)(tileY + map.tileSizeScaled.Height) / formHeight;
			}
			if (vertical < 0) //-down
			{
				newY = (float)(tileY - graphicsDrawer.PlayerHeight - 1) / formHeight;
			}

		}
	}
}
