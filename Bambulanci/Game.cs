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
		//https://stackoverflow.com/questions/1922040/how-to-resize-an-image-c-sharp
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
	class Player //struct?
	{
		int id;

		//coords
		public int x;
		public int y;
		
		//barva --


		//list strel
	}

	class Map
	{
		public int cols;
		public int rows;
		public Size tileSizeScaled;

		private Bitmap[] tiles;
		private int[,] grid;

		private Map() { }
		public static Map GetStandardMap(Form form)
		{
			Map result = new Map();

			Bitmap tileAtlas = new Bitmap(@"D:\Git\Bambulanci\Images\tileAtlas.png"); //prozatim neni v referencich
			int atlasCols = 6;
			int tileCount = 24;
			Size tileSize = new Size(48, 48);

			result.cols = 20; //32
			result.rows = 12; //18
			result.tileSizeScaled = new Size(form.Width / result.cols, form.Height / result.rows);
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

	class Game //nerozlisuji hosta a klienta
	{
		int height;
		int width;
		Map map;
		Player player1;
		public Game(Form form, Map map)
		{
			height = form.Height;
			width = form.Width;
			this.map = map;

			//test
			player1 = new Player();
			player1.x = 100;
			player1.y = 100;
		}

		public void Draw(Graphics g)
		{
			DrawBackground(g);
			//DrawPlayers(g); //--------------
		}

		private void DrawBackground(Graphics g)
		{
			//cache image, then view somehow....-------
			for (int column = 0; column < map.cols; column++)
				for (int row = 0; row < map.rows; row++)
				{
					

					Bitmap tile = map.GetTile(column, row);

					tile.SetResolution(g.DpiX, g.DpiY); //?????------------------------------

					g.DrawImage(tile, column * map.tileSizeScaled.Width, row * map.tileSizeScaled.Height);
				}
		}
		private void DrawPlayers(Graphics g)
		{
			//g.FillRectangle(Brushes.Yellow, new Rectangle(player1.x, player1.y, 50, 50));
		}
	}
}
