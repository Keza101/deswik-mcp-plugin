using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Deswik.Addin;

/// <summary>
/// Deswik Add-in for AI-assisted scheduling operations.
/// This add-in exposes drawing capabilities to the Bridge for AI control.
/// </summary>
public class DeswikAddin
{
    private static DeswikAddin? _instance;
    private DrawingCanvas? _canvas;

    /// <summary>
    /// Gets the singleton instance of this add-in.
    /// </summary>
    public static DeswikAddin Instance => _instance ??= new DeswikAddin();

    /// <summary>
    /// Called by Deswik when the add-in is loaded.
    /// </summary>
    public void OnLoad()
    {
        System.Diagnostics.Debug.WriteLine("[DeswikAddin] Add-in loaded successfully.");
        System.Diagnostics.Debug.WriteLine("[DeswikAddin] Drawing capabilities available.");
    }

    /// <summary>
    /// Called by Deswik when the add-in is unloaded.
    /// </summary>
    public void OnUnload()
    {
        System.Diagnostics.Debug.WriteLine("[DeswikAddin] Add-in unloaded.");
    }

    /// <summary>
    /// Sets the drawing canvas for shape rendering.
    /// This should be called from the Deswik UI initialization.
    /// </summary>
    public void SetCanvas(FrameworkElement canvas)
    {
        _canvas = canvas as DrawingCanvas;
        System.Diagnostics.Debug.WriteLine($"[DeswikAddin] Canvas set: {_canvas?.GetType().Name}");
    }

    /// <summary>
    /// Draws a circle on the Deswik canvas.
    /// </summary>
    public bool DrawCircle(int x, int y, int radius, string color = "Blue")
    {
        try
        {
            var brush = new SolidColorBrush(ParseColor(color));

            if (_canvas != null)
            {
                _canvas.Children.Add(new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Stroke = brush,
                    StrokeThickness = 2,
                    Margin = new Thickness(x - radius, y - radius, 0, 0)
                });
            }

            System.Diagnostics.Debug.WriteLine($"[DeswikAddin] Drew circle at ({x},{y}) r={radius} color={color}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeswikAddin] Error drawing circle: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Draws a rectangle on the Deswik canvas.
    /// </summary>
    public bool DrawRectangle(int x, int y, int width, int height, string color = "Red")
    {
        try
        {
            var brush = new SolidColorBrush(ParseColor(color));

            if (_canvas != null)
            {
                _canvas.Children.Add(new Rectangle
                {
                    Width = width,
                    Height = height,
                    Stroke = brush,
                    StrokeThickness = 2,
                    Margin = new Thickness(x, y, 0, 0)
                });
            }

            System.Diagnostics.Debug.WriteLine($"[DeswikAddin] Drew rectangle at ({x},{y}) {width}x{height} color={color}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeswikAddin] Error drawing rectangle: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Draws a line on the Deswik canvas.
    /// </summary>
    public bool DrawLine(int x1, int y1, int x2, int y2, string color = "Black", int thickness = 2)
    {
        try
        {
            var brush = new SolidColorBrush(ParseColor(color));

            if (_canvas != null)
            {
                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = brush,
                    StrokeThickness = thickness
                };
                _canvas.Children.Add(line);
            }

            System.Diagnostics.Debug.WriteLine($"[DeswikAddin] Drew line from ({x1},{y1}) to ({x2},{y2}) color={color}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeswikAddin] Error drawing line: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Clears all shapes from the canvas.
    /// </summary>
    public void ClearCanvas()
    {
        _canvas?.Children.Clear();
        System.Diagnostics.Debug.WriteLine("[DeswikAddin] Canvas cleared.");
    }

    private static Color ParseColor(string colorName)
    {
        return colorName.ToLowerInvariant() switch
        {
            "red" => Colors.Red,
            "blue" => Colors.Blue,
            "green" => Colors.Green,
            "yellow" => Colors.Yellow,
            "black" => Colors.Black,
            "white" => Colors.White,
            "orange" => Colors.Orange,
            "purple" => Colors.Purple,
            "gray" or "grey" => Colors.Gray,
            _ => (Color)ColorConverter.ConvertFromString(colorName)
        };
    }
}

/// <summary>
/// Simple canvas for drawing shapes.
/// </summary>
public class DrawingCanvas : Canvas
{
    public DrawingCanvas()
    {
        Background = Brushes.Transparent;
    }
}
