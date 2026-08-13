using System;
using System.Net;
using SnipShot.Services;

namespace SnipShot.Tests.Services;

/// <summary>
/// Pruebas de <see cref="UpdateService.ParseReleaseResponse"/>, la parte de la comprobación
/// de actualizaciones que no depende de la red ni del paquete instalado.
/// </summary>
[TestClass]
public class UpdateServiceTests
{
    private static readonly Version Instalada = new(1, 1, 0, 0);

    /// <summary>
    /// Construye una respuesta mínima de la API de releases de GitHub.
    /// </summary>
    private static string Json(string cuerpo) => "{" + cuerpo + "}";

    private static string ReleaseCon(string tag) =>
        Json($"\"tag_name\":\"{tag}\",\"html_url\":\"https://github.com/x/y/releases/tag/{tag}\"");

    #region Comparación de versiones

    [TestMethod]
    [DataRow("v1.2.0", true, "una minor superior es actualización")]
    [DataRow("v2.0.0", true, "una major superior es actualización")]
    [DataRow("v1.1.1", true, "un patch superior es actualización")]
    [DataRow("v1.0.0", false, "una versión anterior no es actualización")]
    [DataRow("v1.0.9", false, "un patch anterior en minor inferior no es actualización")]
    public void CompararVersiones_DetectaSiHayActualizacion(string tag, bool esperado, string motivo)
    {
        var resultado = UpdateService.ParseReleaseResponse(ReleaseCon(tag), Instalada);

        Assert.IsTrue(resultado.Success, "la respuesta debería interpretarse sin error");
        Assert.AreEqual(esperado, resultado.IsUpdateAvailable, motivo);
    }

    /// <summary>
    /// Caso que motivó la normalización: <c>Version.TryParse</c> deja Build y Revision en -1
    /// cuando el tag trae menos de cuatro componentes, y el paquete instalado siempre trae
    /// los cuatro. Sin normalizar, "v1.1.0" se compararía como anterior a 1.1.0.0 porque
    /// enfrenta -1 contra 0.
    /// </summary>
    [TestMethod]
    [DataRow("v1.1.0", "tres componentes iguales a la instalada")]
    [DataRow("v1.1", "dos componentes iguales a la instalada")]
    public void MismaVersionConMenosComponentes_NoSeAnunciaComoActualizacion(string tag, string caso)
    {
        var resultado = UpdateService.ParseReleaseResponse(ReleaseCon(tag), Instalada);

        Assert.IsFalse(resultado.IsUpdateAvailable, caso);
    }

    [TestMethod]
    [DataRow("v1.2.0")]
    [DataRow("V1.2.0")]
    [DataRow("1.2.0")]
    public void PrefijoDeTag_SeAceptaEnCualquierForma(string tag)
    {
        var resultado = UpdateService.ParseReleaseResponse(ReleaseCon(tag), Instalada);

        Assert.IsTrue(resultado.Success);
        Assert.AreEqual(new Version(1, 2, 0), resultado.LatestVersion);
    }

    [TestMethod]
    public void TagNoInterpretable_DevuelveErrorSinLanzar()
    {
        var resultado = UpdateService.ParseReleaseResponse(ReleaseCon("beta-final"), Instalada);

        Assert.IsFalse(resultado.Success);
        Assert.IsFalse(resultado.IsUpdateAvailable);
        StringAssert.Contains(resultado.ErrorMessage!, "beta-final");
    }

    #endregion

    #region Robustez ante respuestas incompletas

    [TestMethod]
    public void SinTagName_DevuelveErrorSinLanzar()
    {
        var resultado = UpdateService.ParseReleaseResponse(Json("\"html_url\":\"https://x\""), Instalada);

        Assert.IsFalse(resultado.Success);
        Assert.IsFalse(resultado.IsUpdateAvailable);
    }

    [TestMethod]
    public void SinHtmlUrl_RecurreALaPaginaDeReleases()
    {
        var resultado = UpdateService.ParseReleaseResponse(Json("\"tag_name\":\"v1.2.0\""), Instalada);

        Assert.IsTrue(resultado.Success);
        Assert.AreEqual(UpdateService.ReleasesPageUrl, resultado.ReleasePageUrl);
    }

    [TestMethod]
    public void SinBody_DejaLasNotasVacias()
    {
        var resultado = UpdateService.ParseReleaseResponse(ReleaseCon("v1.2.0"), Instalada);

        Assert.AreEqual(string.Empty, resultado.ReleaseNotes);
    }

    [TestMethod]
    public void ConBody_DevuelveLasNotas()
    {
        var json = Json("\"tag_name\":\"v1.2.0\",\"body\":\"Corrige el guardado\"");

        var resultado = UpdateService.ParseReleaseResponse(json, Instalada);

        Assert.AreEqual("Corrige el guardado", resultado.ReleaseNotes);
    }

    #endregion

    #region Localización del instalador

    [TestMethod]
    public void ConAssetMsixbundle_DevuelveSuUrlDeDescarga()
    {
        var json = Json(
            "\"tag_name\":\"v1.2.0\",\"assets\":[" +
            "{\"name\":\"notas.txt\",\"browser_download_url\":\"https://x/notas.txt\"}," +
            "{\"name\":\"SnipShot_1.2.0.msixbundle\",\"browser_download_url\":\"https://x/app.msixbundle\"}]");

        var resultado = UpdateService.ParseReleaseResponse(json, Instalada);

        Assert.AreEqual("https://x/app.msixbundle", resultado.DownloadUrl);
    }

    [TestMethod]
    public void SinAssetMsixbundle_DejaLaDescargaEnNulo()
    {
        var json = Json(
            "\"tag_name\":\"v1.2.0\",\"assets\":[" +
            "{\"name\":\"notas.txt\",\"browser_download_url\":\"https://x/notas.txt\"}]");

        var resultado = UpdateService.ParseReleaseResponse(json, Instalada);

        Assert.IsTrue(resultado.Success);
        Assert.IsNull(resultado.DownloadUrl);
    }

    [TestMethod]
    public void ExtensionMsixbundle_SeReconoceSinDistinguirMayusculas()
    {
        var json = Json(
            "\"tag_name\":\"v1.2.0\",\"assets\":[" +
            "{\"name\":\"SnipShot.MSIXBUNDLE\",\"browser_download_url\":\"https://x/app.msixbundle\"}]");

        var resultado = UpdateService.ParseReleaseResponse(json, Instalada);

        Assert.AreEqual("https://x/app.msixbundle", resultado.DownloadUrl);
    }

    [TestMethod]
    public void AssetSinNombre_SeIgnoraSinLanzar()
    {
        var json = Json(
            "\"tag_name\":\"v1.2.0\",\"assets\":[" +
            "{\"browser_download_url\":\"https://x/misterioso\"}," +
            "{\"name\":\"SnipShot.msixbundle\",\"browser_download_url\":\"https://x/app.msixbundle\"}]");

        var resultado = UpdateService.ParseReleaseResponse(json, Instalada);

        Assert.AreEqual("https://x/app.msixbundle", resultado.DownloadUrl);
    }

    [TestMethod]
    public void AssetMsixbundleSinUrl_NoLanzaYDejaLaDescargaEnNulo()
    {
        var json = Json("\"tag_name\":\"v1.2.0\",\"assets\":[{\"name\":\"SnipShot.msixbundle\"}]");

        var resultado = UpdateService.ParseReleaseResponse(json, Instalada);

        Assert.IsTrue(resultado.Success);
        Assert.IsNull(resultado.DownloadUrl);
    }

    [TestMethod]
    public void AssetsQueNoEsUnaLista_SeIgnoraSinLanzar()
    {
        var json = Json("\"tag_name\":\"v1.2.0\",\"assets\":\"ninguno\"");

        var resultado = UpdateService.ParseReleaseResponse(json, Instalada);

        Assert.IsTrue(resultado.Success);
        Assert.IsNull(resultado.DownloadUrl);
    }

    #endregion

    #region Mensajes de error HTTP

    [TestMethod]
    public void RespuestaNotFound_IndicaQueNoHayReleases()
    {
        var mensaje = UpdateService.BuildHttpErrorMessage(HttpStatusCode.NotFound, null, null);

        StringAssert.Contains(mensaje, "No se encontraron releases");
    }

    [TestMethod]
    public void Forbidden_ConLimiteAgotado_IndicaElLimiteYLaHoraDeReinicio()
    {
        // 2026-08-13 17:51 en la zona local, expresado en segundos Unix
        var reinicio = new DateTimeOffset(2026, 8, 13, 17, 51, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 8, 13)));

        var mensaje = UpdateService.BuildHttpErrorMessage(
            HttpStatusCode.Forbidden, "0", reinicio.ToUnixTimeSeconds().ToString());

        StringAssert.Contains(mensaje, "límite");
        StringAssert.Contains(mensaje, "17:51");
    }

    [TestMethod]
    public void Forbidden_SinCabeceraDeLimite_NoSeConfundeConElLimite()
    {
        // Un 403 por permisos no trae x-ratelimit-remaining a 0: no debe anunciarse
        // como limite alcanzado ni prometer una hora de reinicio que no existe.
        var mensaje = UpdateService.BuildHttpErrorMessage(HttpStatusCode.Forbidden, null, null);

        StringAssert.Contains(mensaje, "Forbidden");
        Assert.IsFalse(mensaje.Contains("límite"));
    }

    [TestMethod]
    public void Forbidden_ConPeticionesRestantes_NoSeTrataComoLimite()
    {
        var mensaje = UpdateService.BuildHttpErrorMessage(HttpStatusCode.Forbidden, "42", null);

        StringAssert.Contains(mensaje, "Forbidden");
        Assert.IsFalse(mensaje.Contains("límite"));
    }

    [TestMethod]
    public void TooManyRequests_ConLimiteAgotado_TambienSeReconoce()
    {
        var mensaje = UpdateService.BuildHttpErrorMessage(HttpStatusCode.TooManyRequests, "0", null);

        StringAssert.Contains(mensaje, "límite");
    }

    [TestMethod]
    public void LimiteAgotado_ConReinicioIlegible_OmiteLaHoraSinLanzar()
    {
        var mensaje = UpdateService.BuildHttpErrorMessage(HttpStatusCode.Forbidden, "0", "no-es-un-numero");

        StringAssert.Contains(mensaje, "límite");
        StringAssert.Contains(mensaje, "más tarde");
    }

    [TestMethod]
    public void OtroErrorHttp_MuestraElCodigo()
    {
        var mensaje = UpdateService.BuildHttpErrorMessage(HttpStatusCode.ServiceUnavailable, null, null);

        StringAssert.Contains(mensaje, "ServiceUnavailable");
    }

    #endregion
}
