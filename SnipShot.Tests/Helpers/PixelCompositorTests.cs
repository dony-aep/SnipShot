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
    public void CapaTransparente_NoCambiaLaImagen()
    {
        var capa = new byte[10 * 8 * 4];

        CollectionAssert.AreEqual(
            PixelCompositor.Crop(Imagen(10, 8), 10, 8, 1, 1, 5, 5),
            PixelCompositor.Crop(Imagen(10, 8), 10, 8, 1, 1, 5, 5, capa, 10, 8));
    }

    [TestMethod]
    public void AnotacionOpaca_SustituyeAlFondoEnSuPosicion()
    {
        var capa = new byte[10 * 8 * 4];
        int i = (3 * 10 + 4) * 4; // (4, 3)
        capa[i + 2] = 255; capa[i + 3] = 255;

        var recorte = PixelCompositor.Crop(Imagen(10, 8), 10, 8, 2, 2, 5, 5, capa, 10, 8);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, Pixel(recorte, 5, 2, 1), "la anotación se pinta en su posición de pantalla");
        CollectionAssert.AreEqual(new byte[] { 3, 3, 0, 255 }, Pixel(recorte, 5, 1, 1), "los píxeles vecinos siguen siendo fondo");
    }

    [TestMethod]
    public void AnotacionSemitransparente_SeMezclaConAlfaPremultiplicado()
    {
        // Fondo blanco opaco y rojo al 50 % premultiplicado (R = 128, A = 128).
        var fondo = Enumerable.Repeat((byte)255, 4).ToArray();
        var capa = new byte[] { 0, 0, 128, 128 };

        // Resultado = capa + fondo * (1 - 128/255) ≈ capa + 127.
        CollectionAssert.AreEqual(new byte[] { 127, 127, 255, 255 }, PixelCompositor.Crop(fondo, 1, 1, 0, 0, 1, 1, capa, 1, 1));
    }

    [TestMethod]
    public void CapaMasPequenaQueLaImagen_SeIgnoraFueraDeSusLimites()
    {
        // RenderTargetBitmap puede devolver unos píxeles menos que el fondo por redondeo.
        var capa = Enumerable.Repeat((byte)200, 2 * 2 * 4).ToArray();

        var recorte = PixelCompositor.Crop(Imagen(4, 4), 4, 4, 0, 0, 4, 4, capa, 2, 2);

        CollectionAssert.AreEqual(new byte[] { 200, 200, 200, 255 }, Pixel(recorte, 4, 1, 1), "dentro de la capa");
        CollectionAssert.AreEqual(new byte[] { 3, 3, 0, 255 }, Pixel(recorte, 4, 3, 3), "fuera de la capa queda la imagen");
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
