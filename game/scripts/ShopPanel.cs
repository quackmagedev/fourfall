using Fourfall.Core;
using Godot;

namespace Fourfall.Game;

/// <summary>
/// The Requisition Terminal overlay shown between Quotas. Pure view: renders the
/// terminal's stock and forwards purchase/resume clicks to <see cref="Main"/>,
/// which owns the actual <see cref="RequisitionTerminal"/> state.
/// </summary>
public partial class ShopPanel : CanvasLayer
{
    [Signal]
    public delegate void PurchaseRequestedEventHandler(int index);

    [Signal]
    public delegate void ResumeRequestedEventHandler();

    private VBoxContainer _root = null!;

    public override void _Ready()
    {
        Visible = false;

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);

        _root = new VBoxContainer { CustomMinimumSize = new Vector2(620f, 0f) };
        _root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(_root);
    }

    public void Open(RequisitionTerminal terminal, RunState state, string contextLine)
    {
        Rebuild(terminal, state, contextLine);
        Visible = true;
    }

    public void Refresh(RequisitionTerminal terminal, RunState state, string contextLine) =>
        Rebuild(terminal, state, contextLine);

    public void Close() => Visible = false;

    private void Rebuild(RequisitionTerminal terminal, RunState state, string contextLine)
    {
        foreach (Node child in _root.GetChildren())
        {
            child.QueueFree();
        }

        _root.AddChild(MakeLabel("REQUISITION TERMINAL", 30, new Color("d8a022")));
        _root.AddChild(MakeLabel(contextLine, 18, new Color("9aa7c4")));
        _root.AddChild(MakeLabel($"Credits: {state.Credits}", 24, new Color("ffd866")));
        _root.AddChild(new HSeparator());

        for (int i = 0; i < terminal.Stock.Count; i++)
        {
            _root.AddChild(MakeOfferRow(terminal.Stock[i], i, state.Credits));
        }

        _root.AddChild(new HSeparator());

        var resume = new Button { Text = "RESUME OPERATIONS" };
        resume.AddThemeFontSizeOverride("font_size", 22);
        resume.Pressed += () => EmitSignal(SignalName.ResumeRequested);
        _root.AddChild(resume);
    }

    private Control MakeOfferRow(ShopOffer offer, int index, long credits)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        string tag = offer is TokenOffer ? "[BAG]" : "[WELD]";
        text.AddChild(MakeLabel($"{tag} {offer.Name}", 20, Colors.White));
        text.AddChild(MakeLabel(offer.Description, 15, new Color("8b95ad")));
        row.AddChild(text);

        var buy = new Button
        {
            Text = offer.Sold ? "SOLD" : $"BUY  {offer.Cost}",
            Disabled = offer.Sold || credits < offer.Cost,
            CustomMinimumSize = new Vector2(130f, 0f),
        };
        buy.AddThemeFontSizeOverride("font_size", 18);
        int captured = index;
        buy.Pressed += () => EmitSignal(SignalName.PurchaseRequested, captured);
        row.AddChild(buy);

        return row;
    }

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
