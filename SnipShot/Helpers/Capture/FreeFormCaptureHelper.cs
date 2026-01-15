using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SnipShot.Helpers.Capture
{
    /// <summary>
    /// Utilidades para procesar capturas en modo forma libre.
    /// </summary>
    public static class FreeFormCaptureHelper
    {
        /// <summary>
        /// Calcula el rectángulo delimitador para un conjunto de puntos de pantalla.
        /// </summary>
        public static RectInt32 CalculateBoundingRect(IReadOnlyList<PointInt32> points)
        {
            if (points == null || points.Count == 0)
            {
                return new RectInt32(0, 0, 0, 0);
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
            }

            if (minX == int.MaxValue || minY == int.MaxValue)
            {
                return new RectInt32(0, 0, 0, 0);
            }

            int width = Math.Max(1, (maxX - minX) + 1);
            int height = Math.Max(1, (maxY - minY) + 1);

            return new RectInt32(minX, minY, width, height);
        }

        /// <summary>
        /// Genera un bitmap enmascarado a partir del fondo capturado previamente.
        /// </summary>
        public static async Task<SoftwareBitmap?> CreateMaskedBitmapFromBackgroundAsync(
            SoftwareBitmap? backgroundBitmap,
            RectInt32 virtualBounds,
            IReadOnlyList<PointInt32> polygonPoints)
        {
            if (backgroundBitmap == null || polygonPoints == null || polygonPoints.Count < 3)
            {
                return null;
            }

            var bounds = CalculateBoundingRect(polygonPoints);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return null;
            }

            bool createdConversion = false;
            SoftwareBitmap bitmapForCrop = backgroundBitmap;

            if (backgroundBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                backgroundBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                bitmapForCrop = SoftwareBitmap.Convert(
                    backgroundBitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
                createdConversion = true;
            }

            int relativeX = bounds.X - virtualBounds.X;
            int relativeY = bounds.Y - virtualBounds.Y;

            int cropX = Math.Clamp(relativeX, 0, Math.Max(0, bitmapForCrop.PixelWidth - 1));
            int cropY = Math.Clamp(relativeY, 0, Math.Max(0, bitmapForCrop.PixelHeight - 1));
            int cropWidth = Math.Clamp(bounds.Width, 0, bitmapForCrop.PixelWidth - cropX);
            int cropHeight = Math.Clamp(bounds.Height, 0, bitmapForCrop.PixelHeight - cropY);

            if (cropWidth <= 0 || cropHeight <= 0)
            {
                if (createdConversion)
                {
                    bitmapForCrop.Dispose();
                }

                return null;
            }

            SoftwareBitmap? cropped = null;
            try
            {
                cropped = await CropBitmapAsync(bitmapForCrop, cropX, cropY, cropWidth, cropHeight);
                if (cropped == null)
                {
                    return null;
                }

                var masked = await ApplyMaskAsync(cropped, polygonPoints, bounds);

                if (!ReferenceEquals(masked, cropped))
                {
                    cropped.Dispose();
                }

                return masked;
            }
            finally
            {
                if (createdConversion)
                {
                    bitmapForCrop.Dispose();
                }
            }
        }

        /// <summary>
        /// Aplica una máscara a un bitmap ya capturado de la región delimitada.
        /// </summary>
        public static async Task<SoftwareBitmap?> ApplyMaskToCapturedRegionAsync(
            SoftwareBitmap? regionBitmap,
            IReadOnlyList<PointInt32> polygonPoints,
            RectInt32 regionBounds)
        {
            if (regionBitmap == null || polygonPoints == null || polygonPoints.Count < 3)
            {
                return regionBitmap;
            }

            return await ApplyMaskAsync(regionBitmap, polygonPoints, regionBounds);
        }

        private static async Task<SoftwareBitmap?> CropBitmapAsync(
            SoftwareBitmap source,
            int x,
            int y,
            int width,
            int height)
        {
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(source);
            encoder.BitmapTransform.Bounds = new BitmapBounds
            {
                X = (uint)x,
                Y = (uint)y,
                Width = (uint)width,
                Height = (uint)height
            };
            encoder.IsThumbnailGenerated = false;
            await encoder.FlushAsync();

            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
        }

        private static async Task<SoftwareBitmap?> ApplyMaskAsync(
            SoftwareBitmap bitmap,
            IReadOnlyList<PointInt32> polygonPoints,
            RectInt32 regionBounds)
        {
            if (polygonPoints.Count < 3)
            {
                return bitmap;
            }

            bool createdConversion = false;
            SoftwareBitmap bitmapForRendering = bitmap;

            if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                bitmapForRendering = SoftwareBitmap.Convert(
                    bitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
                createdConversion = true;
            }

            var device = CanvasDevice.GetSharedDevice();

            using var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, bitmapForRendering);
            using var renderTarget = new CanvasRenderTarget(
                device,
                bitmapForRendering.PixelWidth,
                bitmapForRendering.PixelHeight,
                canvasBitmap.Dpi);

            var relativePoints = ConvertToRelativePoints(polygonPoints, regionBounds);
            if (relativePoints.Length < 3)
            {
                if (createdConversion)
                {
                    bitmapForRendering.Dispose();
                }

                return bitmapForRendering;
            }

            using (var geometry = CanvasGeometry.CreatePolygon(device, relativePoints))
            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.Clear(Colors.Transparent);
                using (var layer = ds.CreateLayer(1.0f, geometry))
                {
                    ds.DrawImage(canvasBitmap);
                }
            }

            var masked = await CreateSoftwareBitmapFromRenderTargetAsync(renderTarget);

            if (createdConversion)
            {
                bitmapForRendering.Dispose();
            }

            return masked;
        }

        private static Vector2[] ConvertToRelativePoints(IReadOnlyList<PointInt32> points, RectInt32 bounds)
        {
            var result = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                result[i] = new Vector2(
                    points[i].X - bounds.X,
                    points[i].Y - bounds.Y);
            }

            return result;
        }

        private static async Task<SoftwareBitmap> CreateSoftwareBitmapFromRenderTargetAsync(CanvasRenderTarget renderTarget)
        {
            using var stream = new InMemoryRandomAccessStream();
            await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);

            return await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
        }
    }
}
