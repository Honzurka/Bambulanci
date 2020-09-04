﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

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
		public PlayerMovement Direction { get; private set; }

		public Player(float x, float y, int id = -1, PlayerMovement direction = PlayerMovement.Left)
		{
			this.X = x;
			this.Y = y;
			this.id = id;
			this.Direction = direction;
		}

		/*
		public List<Shot> shots; //public?
		public class Shot
		{

		}*/

		public void MoveByHost(PlayerMovement playerMovement)//host only //colision detection in the future
		{
			if (playerMovement != PlayerMovement.Stay)
				Direction = playerMovement;

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

			if (X + newX >= 0 && X + newX <= 1 - 1 / widthScaling && Y + newY >= 0 && Y + newY <= 1 - 1 / heightScaling)
			{
				X += newX;
				Y += newY;
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

		private int formWidth;
		private int formHeight;

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

			result.formWidth = formWidth;
			result.formHeight = formHeight;

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

			result.wallTiles = new int[] { 6, 7, 8, 9, 18, 19, 20, 21 };
			return result;
		}

		public Bitmap GetTile(int column, int row)
		{
			int tileNum = grid[row, column];
			return tiles[tileNum];
		}


		Random rng = new Random();
		/// <summary>
		/// Return spawn coords on tile that is not a wall.
		/// </summary>
		/// <returns> Coords scaled between 0 and 1. </returns>
		public (float,float) GetSpawnCoords()
		{
			int col = rng.Next(cols);
			int row = rng.Next(rows);

			while (Array.IndexOf(wallTiles, grid[row,col]) != -1)
			{
				col = rng.Next(cols);
				row = rng.Next(rows);

			}
			return ((float)col * tileSizeScaled.Width / formWidth, (float)row * tileSizeScaled.Height / formHeight);
		}
	}

	public class GraphicsDrawer
	{
		private readonly int formWidth;
		private readonly int formHeight;
		private readonly Map map;
		public GraphicsDrawer(int formWidth, int formHeight, Map map)
		{
			this.formWidth = formWidth;
			this.formHeight = formHeight;
			this.map = map;
			playerDesigns = CreatePlayerDesign();
		}

		const int colorsPerPlayer = 4;


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


		private readonly Bitmap[] playerDesigns; //left, up, right, down
		private readonly Brush[] allowedColors = new Brush[] { Brushes.Yellow, Brushes.Red, Brushes.Aqua, Brushes.BlueViolet, Brushes.Chocolate };
		/// <summary>
		/// Creaters array of playerDesigns based on allowedColors.
		/// Player desing of each color occurs 4 times, always rotated by 90 degrees.
		/// In order: Left, Up, Right, Down
		/// </summary>
		private Bitmap[] CreatePlayerDesign()
		{
			const int eyeScaling = 3;

			Bitmap[] result = new Bitmap[allowedColors.Length * colorsPerPlayer];
			for (int i = 0; i < allowedColors.Length; i++)
			{
				Brush playerColor = allowedColors[i];
				int playerWidth = (int)(formWidth/ Player.widthScaling);
				int playerHeight = (int)(formHeight / Player.heightScaling);

				Bitmap bitmap = new Bitmap(playerWidth, playerHeight);
				var g = Graphics.FromImage(bitmap);

				int w = playerWidth / eyeScaling;
				int h = playerHeight / eyeScaling;

				int offset = (playerWidth / 2 - w) / 2;
				g.FillRectangle(playerColor, new Rectangle(0, 0, playerWidth, playerHeight));
				g.FillEllipse(Brushes.Black, new Rectangle(0, offset, w, h));
				g.FillEllipse(Brushes.Black, new Rectangle(0, offset + playerHeight / 2, w, h));

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

		public Bitmap GetPlayerDesign(int designID, byte direction) //can't draw because of client's system of drawing players through queue
		{
			int i = designID * colorsPerPlayer + direction;
			int mod = allowedColors.Length * colorsPerPlayer;
			return playerDesigns[i % mod];
		}

		public void DrawPlayer(Graphics g, Player player)
		{
			int i = player.id * colorsPerPlayer + (byte)player.Direction;
			int mod = allowedColors.Length * colorsPerPlayer;
			Bitmap playerBitmap = playerDesigns[i % mod];
			g.DrawImage(playerBitmap, player.X * formWidth, player.Y * formHeight);
		}
	}

	class Game
	{
		public readonly GraphicsDrawer graphicsDrawer;
		public readonly Map map;
		public Game(int formWidth, int formHeight)
		{
			map = Map.GetStandardMap(formWidth, formHeight); //might be delegate in case of multiple maps
			graphicsDrawer = new GraphicsDrawer(formWidth, formHeight, map);
		}
	}
}
