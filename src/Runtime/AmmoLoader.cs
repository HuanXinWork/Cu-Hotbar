using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknown.Hotbar
{
    /// <summary>主手持枪缺弹时按冷却间隔从身上匹配弹药装填。</summary>
    internal static class AmmoLoader
    {
        private const float ReloadCooldown = 0.35f;
        private static readonly AccessTools.FieldRef<GunScript, float> GasTimeRef =
            AccessTools.FieldRefAccess<GunScript, float>("gasTime");

        private static float _nextReloadAt;

        internal static bool TryAutoReloadHandGun(Body body)
        {
            if (Time.time < _nextReloadAt) return false;
            if (body == null || !body.conscious) return false;

            var hand = body.GetItem(body.handSlot);
            if (hand == null || !hand.TryGetComponent<GunScript>(out var gun)) return false;

            // 半自动/自动枪抛壳后至导气推弹完成（gasTime 置为 -100f）前 racked 暂为 true，排除该窗口防止抢塞散弹
            float gasTime = GasTimeRef != null ? GasTimeRef(gun) : -100f;
            bool cyclingFromMag = gun.firingMode != GunScript.FiringMode.Pump
                && gasTime > -50f && gun.roundsInMag > 0;
            bool needChamber = gun.racked && gun.roundInChamber == GunScript.RoundInChamber.None && !cyclingFromMag;
            bool needMag = !gun.hasMag && gun.feedType == GunScript.FeedType.Mag;
            bool needDirect = gun.feedType == GunScript.FeedType.Direct && gun.roundsInMag < gun.magCapacity;
            if (!needChamber && !needMag && !needDirect) return false;

            // 原版 LoadMag 按 PlayerCamera.main.body.charType 判 Milky 特性，联机客机若房主非 Milky 会失步，故仅本机/房主启用
            bool allowMilkyDirectMag = !MultiplayerCompat.IsClient
                && body.charType == 1 && gun.feedType == GunScript.FeedType.Direct;

            AmmoScript bestMag = null;
            AmmoScript firstRound = null;
            AmmoScript milkyMagFallback = null;

            foreach (var it in body.GetAllItemsThorough())
            {
                if (it == null || it == hand) continue;
                if (!it.TryGetComponent<AmmoScript>(out var a) || a.ammoType != gun.ammoType) continue;

                if (needMag && a.itemType == AmmoScript.AmmoItemType.Magazine && a.rounds > 0)
                {
                    if (bestMag == null || a.rounds > bestMag.rounds) bestMag = a;
                }
                else if (needChamber || needDirect)
                {
                    if (firstRound == null && a.itemType == AmmoScript.AmmoItemType.Round)
                    {
                        firstRound = a;
                    }
                    else if (allowMilkyDirectMag && a.itemType == AmmoScript.AmmoItemType.Magazine && a.rounds > 0)
                    {
                        if (milkyMagFallback == null || a.rounds > milkyMagFallback.rounds)
                            milkyMagFallback = a;
                    }
                }
            }

            // 缺弹匣时优先插满弹匣，否则装填散弹或本机 Milky 直装弹匣
            AmmoScript pick = bestMag != null ? bestMag : (firstRound != null ? firstRound : milkyMagFallback);
            if (pick == null) return false;

            var ammoItem = pick.GetComponent<Item>();
            if (ammoItem == null) return false;

            ModLog.Flow("Reload", "自动装填",
                $"枪械={HotbarRuntime.DescribeItem(hand)} 弹药={HotbarRuntime.DescribeItem(ammoItem)}({pick.rounds}/{pick.maxRounds})");

            // 通过 Body.CombineItems 装填以触发联机同步补丁
            body.CombineItems(hand, ammoItem);
            _nextReloadAt = Time.time + ReloadCooldown;
            return true;
        }
    }
}
