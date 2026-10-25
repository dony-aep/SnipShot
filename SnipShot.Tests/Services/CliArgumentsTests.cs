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
        Assert.AreEqual("window", Aceptar("capture", "window").Mode);
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
    public void ArgumentosInvalidos_SeRechazanConUnMensaje(string[] args, string motivo)
    {
        var error = Rechazar(args);

        Assert.IsFalse(string.IsNullOrWhiteSpace(error), motivo);
    }
}
