using SnipShot.Helpers.Capture;

namespace SnipShot.Tests.Helpers;

/// <summary>
/// Pruebas de <see cref="PixelCompositor"/>, que recorta la captura rectangular del fondo
/// congelado sin tocar lo que se ve en pantalla.
/// </summary>
[TestClass]
public class PixelCompositorTests
{
    /// <summary>
    /// Imagen de <paramref name="ancho"/> × <paramref name="alto"/> en la que cada píxel guarda su
    /// posición (B = x, G = y), para comprobar qué píxeles acaban en el recorte.
    /// </summary>
    private static byte[] Imagen(int ancho, int alto)
    {
        var pixeles = new byte[ancho * alto * 4];
        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                int i = (y * ancho + x) * 4;
                pixeles[i] = (byte)x;
                pixeles[i + 1] = (byte)y;
                pixeles[i + 3] = 255;
            }
        }
        return pixeles;
    }

    private static byte[] Pixel(byte[] imagen, int ancho, int x, int y) => imagen.AsSpan((y * ancho + x) * 4, 4).ToArray();

    [TestMethod]
    public void Recorte_TomaLosPixelesDelRectangulo()
    {
        var recorte = PixelCompositor.Crop(Imagen(10, 8), 10, 8, 3, 2, 4, 5);

        Assert.HasCount(4 * 5 * 4, recorte);
        CollectionAssert.AreEqual(new byte[] { 3, 2, 0, 255 }, Pixel(recorte, 4, 0, 0), "esquina superior izquierda");
        CollectionAssert.AreEqual(new byte[] { 6, 6, 0, 255 }, Pixel(recorte, 4, 3, 4), "esquina inferior derecha");
    }

    [TestMethod]
    public void RecorteDeLaImagenEntera_DevuelveLaMisma()
    {
        var imagen = Imagen(5, 3);

        CollectionAssert.AreEqual(imagen, PixelCompositor.Crop(imagen, 5, 3, 0, 0, 5, 3));
    }

    [TestMethod]
    [DataRow(-1, 0, 2, 2, "x negativa")]
    [DataRow(0, 0, 0, 2, "ancho cero")]
    [DataRow(3, 0, 2, 2, "se sale por la derecha")]
    [DataRow(0, 3, 2, 2, "se sale por abajo")]
    public void RecorteFueraDeLaImagen_Lanza(int x, int y, int ancho, int alto, string caso)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PixelCompositor.Crop(Imagen(4, 4), 4, 4, x, y, ancho, alto), caso);
    }
}
