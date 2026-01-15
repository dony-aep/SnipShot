using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Utilidad para cambiar valores de controles sin disparar eventos.
    /// Útil cuando se necesita actualizar la UI programáticamente sin activar event handlers.
    /// </summary>
    public static class ControlStateManager
    {
        /// <summary>
        /// Cambia el valor de un ToggleSwitch sin disparar el evento Toggled.
        /// </summary>
        /// <param name="toggle">El ToggleSwitch a modificar.</param>
        /// <param name="isOn">El nuevo valor del toggle.</param>
        /// <param name="handler">El event handler a desconectar/reconectar.</param>
        public static void SetToggleSilently(ToggleSwitch toggle, bool isOn, RoutedEventHandler handler)
        {
            if (toggle == null)
            {
                return;
            }

            if (handler != null)
            {
                toggle.Toggled -= handler;
            }

            toggle.IsOn = isOn;

            if (handler != null)
            {
                toggle.Toggled += handler;
            }
        }

        /// <summary>
        /// Cambia el valor de un Slider sin disparar el evento ValueChanged.
        /// </summary>
        /// <param name="slider">El Slider a modificar.</param>
        /// <param name="value">El nuevo valor del slider.</param>
        /// <param name="handler">El event handler a desconectar/reconectar.</param>
        public static void SetSliderValueSilently(Slider slider, double value, RangeBaseValueChangedEventHandler handler)
        {
            if (slider == null)
            {
                return;
            }

            if (handler != null)
            {
                slider.ValueChanged -= handler;
            }

            slider.Value = value;

            if (handler != null)
            {
                slider.ValueChanged += handler;
            }
        }

        /// <summary>
        /// Cambia el texto de un TextBox sin disparar el evento TextChanged.
        /// </summary>
        /// <param name="textBox">El TextBox a modificar.</param>
        /// <param name="text">El nuevo texto.</param>
        /// <param name="handler">El event handler a desconectar/reconectar.</param>
        public static void SetTextSilently(TextBox textBox, string text, TextChangedEventHandler handler)
        {
            if (textBox == null)
            {
                return;
            }

            if (handler != null)
            {
                textBox.TextChanged -= handler;
            }

            textBox.Text = text;

            if (handler != null)
            {
                textBox.TextChanged += handler;
            }
        }

        /// <summary>
        /// Cambia el índice seleccionado de un ComboBox sin disparar el evento SelectionChanged.
        /// </summary>
        /// <param name="comboBox">El ComboBox a modificar.</param>
        /// <param name="selectedIndex">El nuevo índice seleccionado.</param>
        /// <param name="handler">El event handler a desconectar/reconectar.</param>
        public static void SetComboBoxSelectionSilently(ComboBox comboBox, int selectedIndex, SelectionChangedEventHandler handler)
        {
            if (comboBox == null)
            {
                return;
            }

            if (handler != null)
            {
                comboBox.SelectionChanged -= handler;
            }

            comboBox.SelectedIndex = selectedIndex;

            if (handler != null)
            {
                comboBox.SelectionChanged += handler;
            }
        }

        /// <summary>
        /// Cambia el estado de un CheckBox sin disparar eventos.
        /// </summary>
        /// <param name="checkBox">El CheckBox a modificar.</param>
        /// <param name="isChecked">El nuevo estado.</param>
        /// <param name="handler">El event handler a desconectar/reconectar.</param>
        public static void SetCheckBoxSilently(CheckBox checkBox, bool? isChecked, RoutedEventHandler handler)
        {
            if (checkBox == null)
            {
                return;
            }

            if (handler != null)
            {
                checkBox.Checked -= handler;
                checkBox.Unchecked -= handler;
            }

            checkBox.IsChecked = isChecked;

            if (handler != null)
            {
                checkBox.Checked += handler;
                checkBox.Unchecked += handler;
            }
        }

        /// <summary>
        /// Ejecuta una acción sin que se disparen eventos del control.
        /// Útil para casos más complejos donde los métodos específicos no son suficientes.
        /// </summary>
        /// <typeparam name="TControl">Tipo del control.</typeparam>
        /// <typeparam name="TEventHandler">Tipo del event handler.</typeparam>
        /// <param name="control">El control a modificar.</param>
        /// <param name="action">La acción a ejecutar sobre el control.</param>
        /// <param name="removeHandler">Acción para remover el handler.</param>
        /// <param name="addHandler">Acción para agregar el handler.</param>
        public static void ExecuteSilently<TControl>(
            TControl control,
            Action<TControl> action,
            Action removeHandler,
            Action addHandler)
            where TControl : class
        {
            if (control == null || action == null)
            {
                return;
            }

            removeHandler?.Invoke();
            action(control);
            addHandler?.Invoke();
        }
    }
}
