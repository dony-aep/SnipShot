using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.UI;

namespace SnipShot.Services
{
    /// <summary>
    /// Servicio centralizado para la gestión de diálogos en la aplicación.
    /// Proporciona métodos consistentes para mostrar diálogos de error, información, confirmación y personalizados.
    /// </summary>
    public class DialogService
    {
        private readonly XamlRoot _xamlRoot;

        /// <summary>
        /// Inicializa una nueva instancia del servicio de diálogos.
        /// </summary>
        /// <param name="xamlRoot">El XamlRoot asociado a la ventana o control donde se mostrarán los diálogos.</param>
        /// <exception cref="ArgumentNullException">Si xamlRoot es null.</exception>
        public DialogService(XamlRoot xamlRoot)
        {
            _xamlRoot = xamlRoot ?? throw new ArgumentNullException(nameof(xamlRoot));
        }

        /// <summary>
        /// Muestra un diálogo de error con un mensaje.
        /// </summary>
        /// <param name="message">Mensaje de error a mostrar.</param>
        /// <param name="title">Título del diálogo (por defecto "Error").</param>
        public async Task ShowErrorAsync(string message, string title = "Error")
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Ha ocurrido un error desconocido.";
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = _xamlRoot
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// Muestra un diálogo de error a partir de una excepción.
        /// </summary>
        /// <param name="exception">Excepción que causó el error.</param>
        /// <param name="title">Título del diálogo (por defecto "Error").</param>
        /// <param name="customMessage">Mensaje personalizado opcional que se mostrará antes del mensaje de la excepción.</param>
        public async Task ShowErrorAsync(Exception exception, string title = "Error", string? customMessage = null)
        {
            if (exception == null)
            {
                await ShowErrorAsync("Ha ocurrido un error desconocido.", title);
                return;
            }

            var message = customMessage != null
                ? $"{customMessage}\n{exception.Message}"
                : exception.Message;

            await ShowErrorAsync(message, title);
        }

        /// <summary>
        /// Muestra un diálogo de información.
        /// </summary>
        /// <param name="message">Mensaje informativo a mostrar.</param>
        /// <param name="title">Título del diálogo.</param>
        public async Task ShowInfoAsync(string message, string title = "Información")
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = _xamlRoot
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// Muestra un diálogo de éxito.
        /// </summary>
        /// <param name="message">Mensaje de éxito a mostrar.</param>
        /// <param name="title">Título del diálogo (por defecto "Éxito").</param>
        public async Task ShowSuccessAsync(string message, string title = "Éxito")
        {
            await ShowInfoAsync(message, title);
        }

        /// <summary>
        /// Muestra un diálogo de confirmación con botones Sí/No.
        /// </summary>
        /// <param name="message">Mensaje de confirmación.</param>
        /// <param name="title">Título del diálogo (por defecto "Confirmar").</param>
        /// <param name="primaryButtonText">Texto del botón primario (por defecto "Sí").</param>
        /// <param name="secondaryButtonText">Texto del botón secundario (por defecto "No").</param>
        /// <returns>True si el usuario confirmó (botón primario), False en caso contrario.</returns>
        public async Task<bool> ShowConfirmationAsync(
            string message,
            string title = "Confirmar",
            string primaryButtonText = "Sí",
            string secondaryButtonText = "No")
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _xamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        /// <summary>
        /// Muestra un diálogo personalizado con contenido arbitrario.
        /// </summary>
        /// <param name="title">Título del diálogo.</param>
        /// <param name="content">Contenido del diálogo (puede ser string, UIElement, etc.).</param>
        /// <param name="primaryButtonText">Texto del botón primario (opcional).</param>
        /// <param name="secondaryButtonText">Texto del botón secundario (opcional).</param>
        /// <param name="closeButtonText">Texto del botón de cierre (opcional, por defecto "Cancelar").</param>
        /// <param name="defaultButton">Botón predeterminado.</param>
        /// <returns>El resultado del diálogo.</returns>
        public async Task<ContentDialogResult> ShowCustomDialogAsync(
            string title,
            object content,
            string? primaryButtonText = null,
            string? secondaryButtonText = null,
            string? closeButtonText = "Cancelar",
            ContentDialogButton defaultButton = ContentDialogButton.Close)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                DefaultButton = defaultButton,
                XamlRoot = _xamlRoot
            };

            if (!string.IsNullOrEmpty(primaryButtonText))
            {
                dialog.PrimaryButtonText = primaryButtonText;
            }

            if (!string.IsNullOrEmpty(secondaryButtonText))
            {
                dialog.SecondaryButtonText = secondaryButtonText;
            }

            if (!string.IsNullOrEmpty(closeButtonText))
            {
                dialog.CloseButtonText = closeButtonText;
            }

            return await dialog.ShowAsync();
        }

        /// <summary>
        /// Muestra un diálogo de selección de color.
        /// </summary>
        /// <param name="initialColor">Color inicial del selector.</param>
        /// <param name="title">Título del diálogo (por defecto "Seleccionar color").</param>
        /// <param name="enableAlpha">Si se debe habilitar la selección de canal alpha (por defecto true).</param>
        /// <returns>El color seleccionado, o null si el usuario canceló.</returns>
        public async Task<Color?> ShowColorPickerAsync(
            Color initialColor,
            string title = "Seleccionar color",
            bool enableAlpha = true)
        {
            var colorPicker = new ColorPicker
            {
                Color = initialColor,
                ColorSpectrumShape = ColorSpectrumShape.Box,
                IsMoreButtonVisible = false,
                IsColorSliderVisible = true,
                IsColorChannelTextInputVisible = false,
                IsHexInputVisible = false,
                IsAlphaEnabled = enableAlpha,
                IsAlphaSliderVisible = enableAlpha,
                IsAlphaTextInputVisible = false
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = colorPicker,
                PrimaryButtonText = "OK",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _xamlRoot
            };

            // Aplicar estilo de botón de acento si está disponible
            try
            {
                if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var style) && style is Style buttonStyle)
                {
                    dialog.PrimaryButtonStyle = buttonStyle;
                }
            }
            catch
            {
                // Si falla, simplemente no aplicar el estilo
            }

            var result = await dialog.ShowAsync();

            return result == ContentDialogResult.Primary ? colorPicker.Color : null;
        }

        /// <summary>
        /// Muestra un diálogo con un campo de texto para entrada del usuario.
        /// </summary>
        /// <param name="message">Mensaje o pregunta a mostrar.</param>
        /// <param name="title">Título del diálogo.</param>
        /// <param name="placeholderText">Texto de placeholder para el TextBox.</param>
        /// <param name="defaultText">Texto predeterminado en el TextBox.</param>
        /// <returns>El texto ingresado por el usuario, o null si canceló.</returns>
        public async Task<string?> ShowInputDialogAsync(
            string message,
            string title = "Entrada",
            string placeholderText = "",
            string defaultText = "")
        {
            var textBox = new TextBox
            {
                PlaceholderText = placeholderText,
                Text = defaultText,
                AcceptsReturn = false
            };

            var stackPanel = new StackPanel
            {
                Spacing = 12
            };

            if (!string.IsNullOrWhiteSpace(message))
            {
                stackPanel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            }

            stackPanel.Children.Add(textBox);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = stackPanel,
                PrimaryButtonText = "OK",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _xamlRoot
            };

            var result = await dialog.ShowAsync();

            return result == ContentDialogResult.Primary ? textBox.Text : null;
        }
    }
}
