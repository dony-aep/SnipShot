using SnipShot.Cli;
using SnipShot.Services;

namespace SnipShot.Tests.Services;

/// <summary>
/// Pruebas de <see cref="CliArguments"/>: cómo se convierte la línea de comandos de snipshot
/// en una petición. Lo que es válido lo decide la app; aquí solo se comprueba la sintaxis y
/// que las rutas lleguen absolutas.
/// </summary>
[TestClass]
public class CliArgumentsTests
{
    private const string Directorio = @"C:\Proyecto";

    private static ControlRequest Aceptar(params string[] args)
    {
        Assert.IsTrue(CliArguments.TryParse(args, Directorio, out var peticion, out var error), error);
        return peticion;
    }

    private static string Rechazar(params string[] args)
    {
        Assert.IsFalse(CliArguments.TryParse(args, Directorio, out var peticion, out var error), string.Join(' ', args));
        Assert.IsNull(peticion);
        return error;
    }

    [TestMethod]
    public void Status_SeConvierteEnSuPeticion()
    {
        Assert.AreEqual(ControlProtocol.Commands.Status, Aceptar("status").Command);
    }

    [TestMethod]
    public void CapturaSinOpciones_UsaLosValoresPorDefecto()
    {
        var peticion = Aceptar("capture", "fullscreen");

        Assert.AreEqual(ControlProtocol.Commands.Capture, peticion.Command);
        Assert.AreEqual("fullscreen", peticion.Mode);
        Assert.IsNull(peticion.Out, "sin --out se guarda en la carpeta de autoguardado");
        Assert.IsFalse(peticion.Clipboard);
        Assert.AreEqual(0, peticion.DelaySeconds);
    }

    [TestMethod]
    public void CapturaConTodasLasOpciones_LasRecoge()
    {
        var peticion = Aceptar("capture", "fullscreen", "--clipboard", "--delay", "5", "--out", @"D:\salida\a.png");

        Assert.IsTrue(peticion.Clipboard);
        Assert.AreEqual(5, peticion.DelaySeconds);
        Assert.AreEqual(@"D:\salida\a.png", peticion.Out);
    }

    [TestMethod]
    [DataRow("a.png", @"C:\Proyecto\a.png")]
    [DataRow(@"capturas\a.png", @"C:\Proyecto\capturas\a.png")]
    [DataRow(@"..\a.png", @"C:\a.png")]
    public void RutaRelativa_SeResuelveContraElDirectorioDelCli(string ruta, string esperada)
    {
        Assert.AreEqual(esperada, Aceptar("capture", "fullscreen", "--out", ruta).Out);
    }

    [TestMethod]
    public void ModoDesconocido_LlegaTalCualALaApp()
    {
        // La app es la que conoce los modos; el CLI no duplica esa lista.
        Assert.AreEqual("pantalla", Aceptar("capture", "pantalla").Mode);
    }

    [TestMethod]
    public void ListWindows_SeConvierteEnSuPeticion()
    {
        Assert.AreEqual(ControlProtocol.Commands.ListWindows, Aceptar("list-windows").Command);
    }

    [TestMethod]
    [DataRow("12345", 12345L, "decimal, como lo da list-windows")]
    [DataRow("0x3039", 12345L, "hexadecimal, como lo dan Spy++ o winapp")]
    [DataRow("0X3039", 12345L, "prefijo en mayúsculas")]
    public void Hwnd_AceptaDecimalYHexadecimal(string valor, long esperado, string caso)
    {
        Assert.AreEqual(esperado, Aceptar("capture", "window", "--hwnd", valor).Hwnd, caso);
    }

    [TestMethod]
    [DataRow("10,20,300,200", 10, 20, 300, 200, "rectángulo normal")]
    [DataRow("-1920,0,800,600", -1920, 0, 800, 600, "monitor a la izquierda del principal")]
    [DataRow(" 1, 2 ,3,4", 1, 2, 3, 4, "espacios alrededor de los números")]
    public void Rect_SeConvierteEnUnaRegion(string valor, int x, int y, int ancho, int alto, string caso)
    {
        Assert.AreEqual(new RegionRect(x, y, ancho, alto), Aceptar("capture", "region", "--rect", valor).Rect, caso);
    }

    [TestMethod]
    public void RectConVentana_LlegaJuntoAlSelector()
    {
        var peticion = Aceptar("capture", "window", "--hwnd", "198220", "--rect", "0,48,74,984");

        Assert.AreEqual(198220L, peticion.Hwnd);
        Assert.AreEqual(new RegionRect(0, 48, 74, 984), peticion.Rect);
    }

    [TestMethod]
    public void InteractiveYTimeout_SeRecogen()
    {
        var peticion = Aceptar("capture", "window", "--interactive", "--timeout", "45");

        Assert.IsTrue(peticion.Interactive);
        Assert.AreEqual(45, peticion.TimeoutSeconds);
    }

    [TestMethod]
    public void TituloYProceso_LleganTalCual()
    {
        Assert.AreEqual("Bloc de notas", Aceptar("capture", "window", "--title", "Bloc de notas").Title);
        Assert.AreEqual("notepad.exe", Aceptar("capture", "window", "--process", "notepad.exe").Process);
    }

    [TestMethod]
    [DataRow(new string[0], "sin argumentos")]
    [DataRow(new[] { "estado" }, "comando desconocido")]
    [DataRow(new[] { "status", "extra" }, "argumento sobrante")]
    [DataRow(new[] { "capture" }, "capture sin modo")]
    [DataRow(new[] { "capture", "--clipboard" }, "una opción en lugar del modo")]
    [DataRow(new[] { "capture", "fullscreen", "--out" }, "--out sin valor")]
    [DataRow(new[] { "capture", "fullscreen", "--delay" }, "--delay sin valor")]
    [DataRow(new[] { "capture", "fullscreen", "--delay", "tres" }, "retardo que no es un número")]
    [DataRow(new[] { "capture", "fullscreen", "--delay", "-1" }, "retardo con signo")]
    [DataRow(new[] { "capture", "fullscreen", "--format", "jpg" }, "opción desconocida")]
    [DataRow(new[] { "capture", "window", "--hwnd", "ventana" }, "handle que no es un número")]
    [DataRow(new[] { "capture", "window", "--hwnd", "0xZZ" }, "hexadecimal inválido")]
    [DataRow(new[] { "capture", "window", "--title" }, "--title sin valor")]
    [DataRow(new[] { "capture", "window", "--process" }, "--process sin valor")]
    [DataRow(new[] { "list-windows", "--all" }, "list-windows no admite opciones")]
    [DataRow(new[] { "capture", "region", "--rect", "1,2,3" }, "rect con tres números")]
    [DataRow(new[] { "capture", "region", "--rect", "a,b,c,d" }, "rect con texto")]
    [DataRow(new[] { "capture", "region", "--rect" }, "--rect sin valor")]
    [DataRow(new[] { "capture", "region", "--timeout", "dos" }, "timeout que no es un número")]
    public void ArgumentosInvalidos_SeRechazanConUnMensaje(string[] args, string motivo)
    {
        var error = Rechazar(args);

        Assert.IsFalse(string.IsNullOrWhiteSpace(error), motivo);
    }
}
