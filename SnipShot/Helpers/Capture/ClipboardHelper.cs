using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinBuffer = Windows.Storage.Streams.Buffer;

namespace SnipShot.Helpers.Capture
{
    /// <summary>
    /// Helper para operaciones con el portapapeles
    /// </summary>
    public static class ClipboardHelper
 {
        private const int BitmapV5HeaderSize = 124;
      private const int BytesPerPixel = 4;
        private const uint LcsSrGbColorSpace = 0x73524742;
   private const uint LcsGmImagesIntent = 0x00000004;
        private const uint BiRgb = 0;

        // P/Invoke para acceso directo al portapapeles de Windows
  [DllImport("user32.dll", SetLastError = true)]
     private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
  private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        private const uint GMEM_MOVEABLE = 0x0002;
 private const uint CF_DIBV5 = 17;

     private static uint CF_PNG = 0;

        /// <summary>
        /// Copia una imagen al portapapeles con soporte para transparencia
        /// </summary>
        /// <param name="softwareBitmap">Imagen a copiar</param>
        public static async Task CopyImageToClipboardAsync(SoftwareBitmap softwareBitmap)
        {
            if (softwareBitmap == null)
     throw new ArgumentNullException(nameof(softwareBitmap));

      // Registrar formato PNG si a�n no est� registrado
            if (CF_PNG == 0)
            {
                CF_PNG = RegisterClipboardFormat("PNG");
            }

  SoftwareBitmap? bitmapToDispose = null;

     try
     {
     var bitmapForEncoding = EnsurePngCompatibleBitmap(softwareBitmap, out bitmapToDispose);

      // Crear datos DIB con transparencia
       var dibData = await CreateDibDataAsync(bitmapForEncoding);
       if (dibData == null || dibData.Length == 0)
        {
  throw new InvalidOperationException("No se pudo crear datos DIB");
                }

 // Crear datos PNG con transparencia
    byte[]? pngData = null;
   if (CF_PNG != 0)
           {
          pngData = await CreatePngDataAsync(bitmapForEncoding);
    }

 // Copiar al portapapeles usando Win32 API directamente
     await Task.Run(() =>
   {
    if (!OpenClipboard(IntPtr.Zero))
        {
 throw new InvalidOperationException("No se pudo abrir el portapapeles");
    }

            try
  {
          EmptyClipboard();

            // Agregar formato PNG (prioridad m�xima para aplicaciones modernas)
  if (CF_PNG != 0 && pngData != null && pngData.Length > 0)
        {
      IntPtr hGlobalPng = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)pngData.Length);
        if (hGlobalPng != IntPtr.Zero)
     {
           try
    {
       IntPtr pGlobalPng = GlobalLock(hGlobalPng);
  if (pGlobalPng != IntPtr.Zero)
           {
          Marshal.Copy(pngData, 0, pGlobalPng, pngData.Length);
      GlobalUnlock(hGlobalPng);
     SetClipboardData(CF_PNG, hGlobalPng);
   }
 }
     catch
            {
    GlobalUnlock(hGlobalPng);
      }
           }
        }

       // Agregar formato DIB V5 (para compatibilidad)
    IntPtr hGlobalDib = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)dibData.Length);
            if (hGlobalDib == IntPtr.Zero)
               {
       throw new OutOfMemoryException("No se pudo asignar memoria para DIB");
     }

    try
      {
     IntPtr pGlobalDib = GlobalLock(hGlobalDib);
          if (pGlobalDib != IntPtr.Zero)
              {
      Marshal.Copy(dibData, 0, pGlobalDib, dibData.Length);
            GlobalUnlock(hGlobalDib);
    SetClipboardData(CF_DIBV5, hGlobalDib);
  }
       }
          catch
      {
          GlobalUnlock(hGlobalDib);
        throw;
 }
  }
        finally
              {
              CloseClipboard();
          }
    });
     }
      finally
            {
        bitmapToDispose?.Dispose();
  }
        }

        private static SoftwareBitmap EnsurePngCompatibleBitmap(SoftwareBitmap source, out SoftwareBitmap? disposableBitmap)
   {
  disposableBitmap = null;

    if (source.BitmapPixelFormat == BitmapPixelFormat.Bgra8 &&
       source.BitmapAlphaMode == BitmapAlphaMode.Premultiplied)
  {
                return source;
            }

            disposableBitmap = SoftwareBitmap.Convert(
      source,
     BitmapPixelFormat.Bgra8,
   BitmapAlphaMode.Premultiplied);

     return disposableBitmap;
   }

        private static async Task<byte[]?> CreatePngDataAsync(SoftwareBitmap bitmap)
     {
         if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
         {
                return null;
       }

            SoftwareBitmap? bitmapForPng = null;
            try
         {
     if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
   bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
    {
       bitmapForPng = SoftwareBitmap.Convert(
        bitmap,
      BitmapPixelFormat.Bgra8,
          BitmapAlphaMode.Premultiplied);
     }

      var pngSource = bitmapForPng ?? bitmap;

      using var stream = new InMemoryRandomAccessStream();
  BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(pngSource);
          await encoder.FlushAsync();

           stream.Seek(0);
        var pngBytes = new byte[stream.Size];
 using (var reader = new DataReader(stream))
        {
     await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(pngBytes);
      }

       return pngBytes;
          }
  finally
  {
                bitmapForPng?.Dispose();
   }
        }

        private static Task<byte[]?> CreateDibDataAsync(SoftwareBitmap bitmap)
        {
            if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
     {
         return Task.FromResult<byte[]?>(null);
            }

       SoftwareBitmap? bitmapForDib = null;
      try
   {
     if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
    bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
         {
   bitmapForDib = SoftwareBitmap.Convert(
  bitmap,
        BitmapPixelFormat.Bgra8,
           BitmapAlphaMode.Premultiplied);
            }

  var dibSource = bitmapForDib ?? bitmap;

 ulong imageSize64 = (ulong)dibSource.PixelWidth * (ulong)dibSource.PixelHeight * (ulong)BytesPerPixel;
   if (imageSize64 == 0 || imageSize64 > int.MaxValue)
         {
  return Task.FromResult<byte[]?>(null);
        }

                int imageSize = (int)imageSize64;
     var pixelBuffer = new WinBuffer((uint)imageSize) { Length = (uint)imageSize };
         dibSource.CopyToBuffer(pixelBuffer);

         var pixels = new byte[imageSize];
   using (var reader = DataReader.FromBuffer(pixelBuffer))
                {
       reader.ReadBytes(pixels);
     }

       var header = BuildBitmapV5Header(dibSource.PixelWidth, dibSource.PixelHeight, (uint)imageSize);
                var dibBytes = new byte[header.Length + pixels.Length];
        System.Buffer.BlockCopy(header, 0, dibBytes, 0, header.Length);
   System.Buffer.BlockCopy(pixels, 0, dibBytes, header.Length, pixels.Length);

                return Task.FromResult<byte[]?>(dibBytes);
          }
            finally
       {
         bitmapForDib?.Dispose();
   }
        }

 private static byte[] BuildBitmapV5Header(int width, int height, uint imageSize)
        {
            var header = new byte[BitmapV5HeaderSize];
            var span = header.AsSpan();

            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), (uint)BitmapV5HeaderSize);
         BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), width);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8, 4), -height);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(12, 2), 1);
     BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(14, 2), 32);
 BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), BiRgb);
    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), imageSize);
    BinaryPrimitives.WriteInt32LittleEndian(span.Slice(24, 4), 0);
 BinaryPrimitives.WriteInt32LittleEndian(span.Slice(28, 4), 0);
          BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32, 4), 0);
         BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(36, 4), 0);
     BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40, 4), 0);
   BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(44, 4), 0);
      BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(48, 4), 0);
       BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(52, 4), 0);
      BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(56, 4), LcsSrGbColorSpace);
       BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(108, 4), LcsGmImagesIntent);
         BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(112, 4), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(116, 4), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(120, 4), 0);

            return header;
        }

        /// <summary>
        /// Copia texto al portapapeles
        /// </summary>
        /// <param name="text">Texto a copiar</param>
        public static async Task CopyTextToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException(nameof(text));

            await Task.Run(() =>
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    throw new InvalidOperationException("No se pudo abrir el portapapeles");
                }

                try
                {
                    EmptyClipboard();

                    // CF_UNICODETEXT = 13
                    const uint CF_UNICODETEXT = 13;

                    // Convertir texto a bytes Unicode (UTF-16 LE con null terminator)
                    var textBytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
                    
                    // Asignar memoria global
                    var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)textBytes.Length);
                    if (hGlobal == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("No se pudo asignar memoria global");
                    }

                    try
                    {
                        // Bloquear memoria y copiar datos
                        var pGlobal = GlobalLock(hGlobal);
                        if (pGlobal == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("No se pudo bloquear memoria global");
                        }

                        Marshal.Copy(textBytes, 0, pGlobal, textBytes.Length);
                        GlobalUnlock(hGlobal);

                        // Establecer datos en portapapeles
                        if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("No se pudo establecer datos en portapapeles");
                        }
                    }
                    catch
                    {
                        // Si hay error, liberar memoria manualmente
                        // (Normalmente el sistema se encarga cuando SetClipboardData tiene éxito)
                        throw;
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            });
        }
    }
}
