using UnityEngine;

// Adds a score bonus to one selected enclosure. If durationDays <= 0 the
// bonus is permanent; otherwise it expires durationDays after being played.
[CreateAssetMenu(fileName = "AmplifyCard", menuName = "SkyZoo/Cards/Amplify Card")]
public class AmplifyCardData : CardData
{
    public int bonusAmount  = 5;
    public int durationDays = 0;

    public override CardTargetMode TargetMode => CardTargetMode.SelectOneEnclosure;
}
