using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace Bambulanci
{
	class Player //struct?
	{
		int id;

		//coords
		int x;
		int y;
		
		//barva --


		//list strel
	}

	class Map
	{
		public int cols;
		public int rows;
		public int tileSize;
		public int[,] grid;

		private Bitmap tileMap;
		private int tileMapCols;
		//public Image backgroundImage; //prob will be tiles--
		//32x18 tileMap of 64x64 tile blocks
		private Map() { }

		public static Map GetStandardMap(Form form) //form might not be needed
		{
			Map result = new Map();
			result.cols = 16;
			result.rows = 9;
			result.tileSize = 64;
			result.tileMap = new Bitmap(@"D:\Git\Bambulanci\Images\standardTiles.png");
			result.tileMapCols = 8;
			result.grid = new int[,]
			{
				{ 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
				{ 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
				{ 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
				{ 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
				{ 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
				{ 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
				{ 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
				{ 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
				{ 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
			};

			return result;
		}

		public Bitmap GetTile(int column, int row)
		{
			int tileNum = grid[row, column];

			int r = tileNum % tileMapCols; //tile row element number---
			int c = tileNum / tileMapCols;

			Rectangle cloneRect = new Rectangle(r * tileSize, c * tileSize, tileSize, tileSize);
			Bitmap result = tileMap.Clone(cloneRect, tileMap.PixelFormat); //pixelformat???

			return result;
		}
	}

	class Game //nerozlisuji hosta a klienta
	{
		int height;
		int width;
		Map map;
		public Game(Form form, Map map)
		{
			height = form.Height;
			width = form.Width;
			this.map = map;

			//form.BackgroundImage = map.backgroundImage;
		}

		//mapa

		public void Draw(Graphics g)
		{
			DrawTileMap(g);

			//g.FillRectangle(Brushes.Black, new Rectangle(10, 10, 50, 50));
		}

		private void DrawTileMap(Graphics g)
		{
			for (int column = 0; column < map.cols; column++)
				for (int row = 0; row < map.rows; row++)
				{
					Bitmap tile = map.GetTile(column, row);
					int x = row * map.tileSize;
					int y = column * map.tileSize;
					g.DrawImage(tile, x, y);
				}
		}
	}
}
