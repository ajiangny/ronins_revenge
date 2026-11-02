using UnityEngine;

// The one colour language every enemy and boss shot speaks, read off its Arrow flags (Arrow.Dress draws it as an edge
// round the art and a halo behind): white can be cut or parried, red can only be parried (blades break on it),
// violet can only be dodged, and bronze is the parry that opens a way (a bell's Resonance).
public enum ShotClass { Open, Guarded, Sealed, Bell }

public static class ShotClasses
{
    public static readonly Color OpenEdge = new Color(1f, 1f, 1f, 0.9f);
    public static readonly Color OpenShimmer = new Color(0.75f, 0.9f, 1f, 0.9f);
    public static readonly Color OpenGlow = new Color(1f, 1f, 1f, 0.35f);

    public static readonly Color GuardedEdge = new Color(1f, 0.12f, 0.15f);
    public static readonly Color GuardedShimmer = new Color(1f, 0.6f, 0.45f);
    public static readonly Color GuardedGlow = new Color(1f, 0.1f, 0.2f, 0.6f);

    // A deep violet, darker than Statuses.JoltColor, so a dodge-only shot isn't mistaken for a Storm spark.
    public static readonly Color SealedEdge = new Color(0.55f, 0.15f, 0.95f);
    public static readonly Color SealedShimmer = new Color(0.85f, 0.55f, 1f);
    public static readonly Color SealedGlow = new Color(0.5f, 0.1f, 0.9f, 0.6f);

    public static Color BellEdge => BulletHell.Bronze;
    public static readonly Color BellShimmer = new Color(1f, 0.85f, 0.55f);

    // A bell rings whatever else it is; a dodge-only shot is sealed even though it's unbreakable too.
    public static ShotClass Of(Arrow a) => a.Resonant ? ShotClass.Bell
        : a.Unparryable ? ShotClass.Sealed
        : (a.Unbreakable || a.Reflectable) ? ShotClass.Guarded
        : ShotClass.Open;

    public static Color Edge(ShotClass c) => c switch
    {
        ShotClass.Guarded => GuardedEdge,
        ShotClass.Sealed => SealedEdge,
        ShotClass.Bell => BellEdge,
        _ => OpenEdge,
    };

    public static Color Shimmer(ShotClass c) => c switch
    {
        ShotClass.Guarded => GuardedShimmer,
        ShotClass.Sealed => SealedShimmer,
        ShotClass.Bell => BellShimmer,
        _ => OpenShimmer,
    };

    public static Color Glow(ShotClass c) => c switch
    {
        ShotClass.Guarded => GuardedGlow,
        ShotClass.Sealed => SealedGlow,
        _ => OpenGlow,
    };
}
