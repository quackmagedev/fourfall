using Fourfall.Core;
using Godot;

namespace Fourfall.Game;

/// <summary>
/// Visual for a single token. Self-drawn (no textures): color comes from the match
/// color, shape accents distinguish the rule-breaker kinds at a glance.
/// </summary>
public partial class TokenView : Node2D
{
    public const float Radius = 32f;

    private TokenColor _color = TokenColor.Red;
    private TokenKind _kind = TokenKind.Standard;

    // Indexed by TokenColor: Red, Yellow, Blue, Green, Purple.
    private static readonly Color[] Palette =
    {
        new("e0524d"),
        new("f0c33c"),
        new("4d8fe0"),
        new("57c478"),
        new("a06ad6"),
    };

    public void SetToken(Token token)
    {
        _color = token.Color;
        _kind = token.Kind;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Color body = Palette[(int)_color];

        switch (_kind)
        {
            case TokenKind.Glass:
                // Translucent body with a hard rim and a specular streak.
                DrawCircle(Vector2.Zero, Radius, new Color(body.R, body.G, body.B, 0.4f));
                DrawArc(Vector2.Zero, Radius - 1.5f, 0f, Mathf.Tau, 48, body, 3f);
                DrawLine(new Vector2(-14f, -12f), new Vector2(-4f, -2f), new Color(1f, 1f, 1f, 0.8f), 3f);
                break;

            case TokenKind.Iron:
                // Darkened body inside a heavy slate ring.
                DrawCircle(Vector2.Zero, Radius, body.Darkened(0.35f));
                DrawArc(Vector2.Zero, Radius - 4f, 0f, Mathf.Tau, 48, new Color("3a3f47"), 7f);
                break;

            case TokenKind.Slag:
                // Industrial debris: dull gray slab with rivets, ignores match color.
                DrawCircle(Vector2.Zero, Radius, new Color("4a4e55"));
                DrawArc(Vector2.Zero, Radius - 3f, 0f, Mathf.Tau, 48, new Color("2e3138"), 5f);
                foreach (Vector2 rivet in new[]
                {
                    new Vector2(-14f, -14f),
                    new Vector2(14f, -14f),
                    new Vector2(-14f, 14f),
                    new Vector2(14f, 14f),
                })
                {
                    DrawCircle(rivet, 3.5f, new Color("6c727b"));
                }

                break;

            case TokenKind.Debris:
                // Soft rubble: muted gray-brown slab with a crack, no match color.
                DrawCircle(Vector2.Zero, Radius, new Color("5a4e42"));
                DrawArc(Vector2.Zero, Radius - 3f, 0f, Mathf.Tau, 48, new Color("3b3028"), 5f);
                DrawLine(new Vector2(-8f, -16f), new Vector2(4f, 0f), new Color("2a2018"), 2.5f);
                DrawLine(new Vector2(4f, 0f), new Vector2(-2f, 16f), new Color("2a2018"), 2.5f);
                break;

            case TokenKind.Drain:
                // Full body with a dark down-arrow stamped in the middle.
                DrawCircle(Vector2.Zero, Radius, body);
                DrawPolygon(
                    new[]
                    {
                        new Vector2(-12f, -8f),
                        new Vector2(12f, -8f),
                        new Vector2(0f, 14f),
                    },
                    new[] { new Color(0f, 0f, 0f, 0.55f) });
                break;

            default:
                DrawCircle(Vector2.Zero, Radius, body);
                DrawCircle(new Vector2(-9f, -9f), Radius * 0.28f, body.Lightened(0.35f));
                break;
        }
    }
}
