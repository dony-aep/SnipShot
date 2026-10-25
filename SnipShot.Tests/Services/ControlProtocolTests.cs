using SnipShot.Services;

namespace SnipShot.Tests.Services;

/// <summary>
/// Pruebas de <see cref="ControlProtocol"/>, el formato de los mensajes entre el comando
/// snipshot y la app. La app responde a lo que no entiende en vez de cerrar la conexión, así
/// que cada entrada inválida tiene que producir un código de error concreto.
/// </summary>
[TestClass]
public class ControlProtocolTests
{
    private static void AssertRechazada(string? linea, string codigoEsperado, string motivo)
    {
        Assert.IsFalse(ControlProtocol.TryParseRequest(linea, out var peticion, out var error), motivo);
        Assert.IsNull(peticion, motivo);
        Assert.AreEqual(codigoEsperado, error.Code, motivo);
    }

    #region Peticiones válidas

    [TestMethod]
    public void PeticionStatus_SeAcepta()
    {
        Assert.IsTrue(ControlProtocol.TryParseRequest("{\"version\":1,\"command\":\"status\"}", out var peticion, out _));
        Assert.AreEqual(ControlProtocol.Commands.Status, peticion.Command);
    }

    [TestMethod]
    public void PeticionSerializadaPorElCli_LaEntiendeLaApp()
    {
        var linea = ControlProtocol.SerializeRequest(new ControlRequest { Command = ControlProtocol.Commands.Status });

        Assert.IsTrue(ControlProtocol.TryParseRequest(linea, out var peticion, out _), linea);
        Assert.AreEqual(ControlProtocol.Version, peticion.Version);
    }

    [TestMethod]
    public void CamposDesconocidos_SeIgnoran()
    {
        // Un CLI más nuevo puede mandar opciones que esta app aún no conoce.
        var aceptada = ControlProtocol.TryParseRequest("{\"version\":1,\"command\":\"status\",\"extra\":true}", out _, out _);

        Assert.IsTrue(aceptada);
    }

    #endregion

    #region Peticiones inválidas

    [TestMethod]
    [DataRow(null, "nada")]
    [DataRow("", "línea vacía")]
    [DataRow("   ", "solo espacios")]
    [DataRow("{", "JSON cortado")]
    [DataRow("status", "texto plano")]
    [DataRow("[]", "array en vez de objeto")]
    [DataRow("null", "literal null")]
    [DataRow("42", "número suelto")]
    public void EntradaQueNoEsUnObjetoJson_EsPeticionInvalida(string? linea, string motivo)
    {
        AssertRechazada(linea, ControlProtocol.ErrorCodes.InvalidRequest, motivo);
    }

    [TestMethod]
    [DataRow("{\"command\":\"status\"}", "falta la versión")]
    [DataRow("{\"version\":1}", "falta el comando")]
    [DataRow("{\"version\":1,\"command\":\"\"}", "comando vacío")]
    [DataRow("{\"version\":\"1\",\"command\":\"status\"}", "versión como texto")]
    [DataRow("{\"version\":1,\"command\":5}", "comando como número")]
    public void CamposObligatoriosAusentesOMalTipados_EsPeticionInvalida(string linea, string motivo)
    {
        AssertRechazada(linea, ControlProtocol.ErrorCodes.InvalidRequest, motivo);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void OtraVersionDelProtocolo_SeRechazaConSuPropioCodigo(int version)
    {
        AssertRechazada($"{{\"version\":{version},\"command\":\"status\"}}",
            ControlProtocol.ErrorCodes.UnsupportedVersion, $"versión {version}");
    }

    [TestMethod]
    [DataRow("capture", "comando que no existe todavía")]
    [DataRow("STATUS", "los comandos distinguen mayúsculas")]
    public void ComandoDesconocido_SeRechazaConSuPropioCodigo(string comando, string motivo)
    {
        AssertRechazada($"{{\"version\":1,\"command\":\"{comando}\"}}", ControlProtocol.ErrorCodes.UnknownCommand, motivo);
    }

    #endregion

    #region Respuestas

    [TestMethod]
    public void RespuestaDeStatus_SobreviveAlViajeDeIdaYVuelta()
    {
        var enviada = new ControlResponse { Ok = true, Status = new StatusInfo("1.2.0.1", 4284, @"C:\Users\x\Pictures\SnipShot") };

        var recibida = ControlProtocol.ParseResponse(ControlProtocol.SerializeResponse(enviada));

        Assert.IsNotNull(recibida);
        Assert.IsTrue(recibida.Ok);
        Assert.IsNull(recibida.Error);
        Assert.AreEqual(enviada.Status, recibida.Status);
    }

    [TestMethod]
    public void RespuestaDeError_ConservaCodigoYMensaje_YOmiteLosDatos()
    {
        var linea = ControlProtocol.SerializeResponse(ControlResponse.Failure(ControlProtocol.ErrorCodes.Disabled, "Control externo desactivado"));

        var recibida = ControlProtocol.ParseResponse(linea);

        Assert.IsNotNull(recibida);
        Assert.IsFalse(recibida.Ok);
        Assert.AreEqual(new ControlError(ControlProtocol.ErrorCodes.Disabled, "Control externo desactivado"), recibida.Error);
        Assert.DoesNotContain("\"status\"", linea, "un campo nulo no debería escribirse");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("{\"ok\":true}")]
    [DataRow("<html>")]
    public void RespuestaIlegible_DevuelveNull(string? linea)
    {
        Assert.IsNull(ControlProtocol.ParseResponse(linea));
    }

    #endregion

    #region Códigos de salida y nombre del pipe

    [TestMethod]
    [DataRow(ControlProtocol.ErrorCodes.InvalidRequest, 2)]
    [DataRow(ControlProtocol.ErrorCodes.UnsupportedVersion, 2)]
    [DataRow(ControlProtocol.ErrorCodes.UnknownCommand, 2)]
    [DataRow(ControlProtocol.ErrorCodes.NotRunning, 4)]
    [DataRow(ControlProtocol.ErrorCodes.AccessDenied, 4)]
    [DataRow(ControlProtocol.ErrorCodes.Disabled, 4)]
    [DataRow(ControlProtocol.ErrorCodes.Internal, 1)]
    [DataRow("codigo_que_no_existe", 1)]
    public void CodigoDeError_SeTraduceAlCodigoDeSalidaDocumentado(string codigo, int salida)
    {
        Assert.AreEqual(salida, ControlProtocol.GetExitCode(ControlResponse.Failure(codigo, "x")));
    }

    [TestMethod]
    public void RespuestaCorrecta_SaleConCero()
    {
        Assert.AreEqual(0, ControlProtocol.GetExitCode(new ControlResponse { Ok = true }));
    }

    [TestMethod]
    public void NombreDelPipe_DependeDeLaSesion()
    {
        Assert.AreNotEqual(ControlProtocol.GetPipeName(1), ControlProtocol.GetPipeName(2));
        StringAssert.Contains(ControlProtocol.GetPipeName(7), "7");
    }

    #endregion
}
