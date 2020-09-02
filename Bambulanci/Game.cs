using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

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

			destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

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
	public class Player
	{
		//int id;
		//bool isAlive;

		const float widthScaling = 32;
		const float heightScaling = 18;

		//coords between 0 and 1
		public float x; //get only or private --should be
		public float y;


		private int formWidth;
		private int formHeight;

		private float speed = 0.01f;
		public PlayerMovement direction;// = PlayerMovement.Left; //implicit value to avoid bugs

		public static Bitmap[] playerDesigns;//left,up,right,down //public static so i can point to them from server's message

		public Player(int formWidth, int formHeight, float x, float y, Brush playerColor)
		{
			this.formWidth = formWidth;
			this.formHeight = formHeight;
			this.x = x;
			this.y = y;
			//playerDesigns = Player.CreatePlayerDesign(formWidth,formHeight, playerColor);
		}

		public static Bitmap[] CreatePlayerDesign(int formWidth, int formHeight, Brush playerColor)
		{
			int playerWidth = (int)(formWidth / widthScaling);
			int playerHeight = (int)(formHeight / heightScaling);

			Bitmap b = new Bitmap(playerWidth, playerHeight);
			var g = Graphics.FromImage(b);

			int w = playerWidth / 3;
			int h = playerHeight / 3;
			int offset = (playerWidth / 2 - w) / 2;
			g.FillRectangle(playerColor, new Rectangle(0, 0, playerWidth, playerHeight));
			g.FillEllipse(Brushes.Black, new Rectangle(0, offset, w, h));
			g.FillEllipse(Brushes.Black, new Rectangle(0, offset + playerHeight / 2, w, h));

			Bitmap b90 = (Bitmap)b.Clone();
			b90.RotateFlip(RotateFlipType.Rotate90FlipNone);

			Bitmap b180 = (Bitmap)b.Clone();
			b180.RotateFlip(RotateFlipType.Rotate180FlipNone);

			Bitmap b270 = (Bitmap)b.Clone();
			b270.RotateFlip(RotateFlipType.Rotate270FlipNone);

			return new Bitmap[] { b, b90, b180, b270 };
		}

		
		//list strel--------
		public List<Shot> shots; //public?
		public class Shot
		{

		}


		public void Move(PlayerMovement playerMovement)
		{
			direction = playerMovement;
			Console.WriteLine($"writing playerMovement : {playerMovement}");
			/*
			if (this.direction != PlayerMovement.Stay)
				this.direction = playerMovement;
			*/

			float newX = 0;
			float newY = 0;

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

			if (x + newX >= 0 && x + newX <= 1 - 1 / widthScaling && y + newY >= 0 && y + newY <= 1 - 1 / heightScaling)
			{
				x += newX;
				y += newY;
				//Console.WriteLine($"x:{x} y:{y}");
			}
		}

		/*public void Draw(Graphics g)
		{
			byte playerDirection = (byte)direction;
			g.DrawImage(playerDesigns[playerDirection], x * formWidth, y * formHeight);
		}*/
	}

	class Map
	{
		public int cols;
		public int rows;
		public Size tileSizeScaled;

		private Bitmap[] tiles;
		private int[,] grid;

		private Map() { }
		public static Map GetStandardMap(int formWidth, int formHeight)
		{
			Map result = new Map();

			Bitmap tileAtlas = new Bitmap(@"D:\Git\Bambulanci\Images\tileAtlas.png"); //prozatim neni v referencich
			int atlasCols = 6;
			int tileCount = 24;
			Size tileSize = new Size(48, 48);

			result.cols = 20;
			result.rows = 12;
			result.tileSizeScaled = new Size(formWidth / result.cols, formHeight / result.rows);
			
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
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,19 },
				{ 9,20,20,20,20,20,20,20,20,20,20,20,20,20,20,20,20,20,20,8 },
			};

			return result;
		}

		public Bitmap GetTile(int column, int row)
		{
			int tileNum = grid[row, column];
			return tiles[tileNum];
		}
	}

	public class Game //nerozlisuji hosta a klienta??????----------
	{
		int formHeight;
		int formWidth;

		Map map;
		public List<ClientInfo> clientInfo;
		public Game(int formWidth, int formHeight, List<ClientInfo> clientInfo)
		{
			this.formWidth = formWidth;
			this.formHeight = formHeight;
			this.map = Map.GetStandardMap(formWidth, formHeight);
			this.clientInfo = clientInfo;
			Random rng = new Random();

			if (clientInfo != null) //host only
			{
				foreach (var client in clientInfo)
				{
					client.player = new Player(formWidth, formHeight, (float)rng.NextDouble(), (float)rng.NextDouble(), Brushes.Yellow);
				}
			}
		}

		public void DrawBackground(Graphics g)
		{
			for (int column = 0; column < map.cols; column++)
				for (int row = 0; row < map.rows; row++)
				{
					Bitmap tile = map.GetTile(column, row);
					tile.SetResolution(g.DpiX, g.DpiY); //?????------------------------------
					g.DrawImage(tile, column * map.tileSizeScaled.Width, row * map.tileSizeScaled.Height);
				}
		}
	}
}
