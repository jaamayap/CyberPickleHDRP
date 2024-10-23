using System;
using System.Linq;
using InfinityPBR;
using UnityEditor;
using UnityEngine;

public class LayerSelectionDialog : EditorWindow
{
    private int _layerSelection;
    private string _message = ""; // Field to store the message
    private Action _onCancel;
    private Action<string> _onOk;

    private void OnGUI()
    {
        InfinityEditor.Label($"{_message}", false, true, true);

        _layerSelection = InfinityEditor.Popup("Layer", _layerSelection, LayerNames());

        GUILayout.FlexibleSpace();

        InfinityEditor.StartRow();
        if (InfinityEditor.Button("Cancel"))
        {
            _onCancel?.Invoke();
            Close();
        }

        if (InfinityEditor.Button("OK"))
        {
            var selectedLayerName = LayerNames()[_layerSelection];
            _onOk?.Invoke(selectedLayerName);
            Close();
        }

        InfinityEditor.EndRow();
    }

    // Method to open the dialog
    public static void ShowDialog(string title, string message, string defaultValue, Action<string> onOk,
        Action onCancel, int width = 500, int height = 200)
    {
        var window = CreateInstance<LayerSelectionDialog>();
        window.titleContent = new GUIContent(title);
        window._message = message; // Store the message
        window._onOk = onOk;
        window._onCancel = onCancel;

        // Set the initial layer selection based on defaultValue
        var layers = window.LayerNames();
        window._layerSelection = layers.ToList().IndexOf(defaultValue);
        if (window._layerSelection == -1) window._layerSelection = 0; // Default to 0 if not found

        window.minSize = new Vector2(width, height);
        window.maxSize = window.minSize;

        window.ShowUtility();
    }

    private string[] LayerNames()
    {
        // Return a list of all the layer names
        return Enumerable.Range(0, 32).Select(LayerMask.LayerToName).Where(name => !string.IsNullOrEmpty(name))
            .ToArray();
    }
}