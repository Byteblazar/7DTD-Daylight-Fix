using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace DaylightFix
{
    public class DaylightFix : IModApi
    {
        public void InitMod(Mod mod)
        {
            new Harmony(GetType().ToString()).PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(World))]
    internal class HarmonyPatches_DuskDawnInit
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(World.DuskDawnInit))]
        static void Postfix_DuskDawnInit(ref World __instance)
        {
            int noon = 12;
            int mod = GameStats.GetInt(EnumGameStats.DayLightLength) / 2;
            __instance.DawnHour = Mathf.Clamp(noon - mod, 0, 23);
            __instance.DuskHour = Mathf.Clamp(noon + mod, 0, 23);
            Log.Out($"[Daylight Fix] Dawn: {__instance.DawnHour}:00. Dusk: {__instance.DuskHour}:00.");
        }
    }

    [HarmonyPatch(typeof(SkyManager))]
    internal class HarmonyPatches_Sky
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(SkyManager.UpdateSunMoonAngles))]
        static bool Prefix_UpdateSunMoonAngles(ref SkyManager __instance)
        {
            if (!(bool)SkyManager.sunLight || !(bool)SkyManager.moonLight)
                return false;
            int noon = 12;
            int mod = GameStats.GetInt(EnumGameStats.DayLightLength) / 2;
            SkyManager.dawnTime = Mathf.Clamp(noon - mod, 0.0f, 23.999f);
            SkyManager.duskTime = Mathf.Clamp(noon + mod, 0.0f, 23.999f);
            if ((double)Time.time - SkyManager.worldRotationTime >= 0.20000000298023224 || SkyManager.bUpdateSunMoonNow)
            {
                SkyManager.worldRotationTime = Time.time;
                SkyManager.bUpdateSunMoonNow = false;
                float num2 = SkyManager.TimeOfDay();
                if ((double)num2 >= SkyManager.dawnTime && (double)num2 < SkyManager.duskTime)
                {
                    SkyManager.worldRotationTarget = (float)(((double)num2 - SkyManager.dawnTime) / (SkyManager.duskTime - (double)SkyManager.dawnTime));
                }
                else
                {
                    float num3 = 24f - SkyManager.duskTime;
                    float num4 = num3 + SkyManager.dawnTime;
                    SkyManager.worldRotationTarget = (double)num2 >= SkyManager.dawnTime ? (num2 - SkyManager.duskTime) / num4 : (num3 + num2) / num4;
                    ++SkyManager.worldRotationTarget;
                }
                SkyManager.worldRotationTarget *= 0.5f;
                SkyManager.worldRotationTarget = Mathf.Clamp01(SkyManager.worldRotationTarget);
            }
            float num5 = SkyManager.worldRotationTarget - SkyManager.worldRotation;
            float worldRotationTarget = SkyManager.worldRotationTarget;
            if ((double)num5 < -0.5)
                ++worldRotationTarget;
            else if ((double)num5 > 0.5)
                --worldRotationTarget;
            SkyManager.worldRotation = Mathf.Lerp(SkyManager.worldRotation, worldRotationTarget, 0.05f);
            if (SkyManager.worldRotation < 0.0)
                ++SkyManager.worldRotation;
            else if (SkyManager.worldRotation >= 1.0)
                --SkyManager.worldRotation;
            SkyManager.dayPercent = SkyManager.CalcDayPercent();
            double angle1 = SkyManager.worldRotation * 360.0;
            SkyManager.sunDirV = Quaternion.AngleAxis((float)angle1, __instance.sunAxis) * __instance.sunStartV;
            SkyManager.moonLightRot = Quaternion.LookRotation(Quaternion.AngleAxis((float)angle1, __instance.sunAxis) * __instance.moonStartV);
            float angle2 = SkyManager.worldRotation * 360f;
            if (SkyManager.sunIntensity >= 1.0 / 1000.0)
            {
                if ((double)angle2 < 14.0)
                    angle2 = 14f;
                if ((double)angle2 > 166.0)
                    angle2 = 166f;
                Vector3 eulerAngles = Quaternion.LookRotation(Quaternion.AngleAxis(angle2, __instance.sunAxis) * __instance.sunStartV).eulerAngles;
                SkyManager.sunLightT.localEulerAngles = eulerAngles;
                SkyManager.sunLight.shadowStrength = 1f;
                SkyManager.sunLight.shadows = SkyManager.sunIntensity > 0.0 ? LightShadows.Soft : LightShadows.None;
                SkyManager.moonLight.enabled = false;
            }
            else if ((double)SkyManager.moonLightColor.grayscale > 0.0)
            {
                if ((double)angle2 < 166.0)
                    angle2 = 166f;
                if ((double)angle2 > 346.0)
                    angle2 = 346f;
                Vector3 eulerAngles = Quaternion.LookRotation(Quaternion.AngleAxis(angle2, __instance.sunAxis) * __instance.moonStartV).eulerAngles;
                SkyManager.moonLightT.localEulerAngles = eulerAngles;
                float num6 = SkyManager.fogLightScale * SkyManager.moonBright * Utils.FastLerp(0.2f, 1f, GamePrefs.GetFloat(EnumGamePrefs.OptionsGfxBrightness) * 2f);
                SkyManager.moonLight.intensity = num6;
                SkyManager.moonLight.color = SkyManager.moonLightColor;
                SkyManager.moonLight.shadowStrength = 1f;
                SkyManager.moonLight.shadows = (double)num6 > 0.0 ? LightShadows.Soft : LightShadows.None;
                SkyManager.moonLight.enabled = true;
            }
            else
                SkyManager.moonLight.enabled = false;
            SkyManager.sunMoonDirV = SkyManager.sunDirV;
            if (SkyManager.sunIntensity < 1.0 / 1000.0)
                SkyManager.sunMoonDirV = SkyManager.moonLightRot * Vector3.forward;
            if (!GameManager.IsDedicatedServer && (bool)SkyManager.mainCamera)
            {
                Vector3 position = SkyManager.mainCamera.transform.position;
                if ((bool)SkyManager.moonSpriteT)
                {
                    SkyManager.moonSpriteT.position = SkyManager.moonLightRot * Vector3.forward * -45000f;
                    SkyManager.moonSpriteT.rotation = Quaternion.LookRotation(SkyManager.moonSpriteT.position, Vector3.up);
                    SkyManager.moonSpriteT.position += position;
                    float num7 = 6857.143f;
                    if (SkyManager.IsBloodMoonVisible())
                        num7 *= 1.3f;
                    SkyManager.moonSpriteT.localScale = new Vector3(num7, num7, num7);
                }
                __instance.UpdateSunShaftSettings();
            }
            SkyManager.atmosphereSphere.Rotate(__instance.starAxis, SkyManager.worldRotation * 0.004f);
            if (!__instance.bUpdateShaders || !(bool)SkyManager.cloudsSphereMtrl)
                return false;
            SkyManager.cloudsSphereMtrl.SetVector("_SunDir", (Vector4)SkyManager.sunDirV);
            SkyManager.cloudsSphereMtrl.SetVector("_SunMoonDir", (Vector4)SkyManager.sunMoonDirV);
            return false;
        }
    }
}
