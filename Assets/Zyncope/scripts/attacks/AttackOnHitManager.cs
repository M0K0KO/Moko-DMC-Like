using UnityEngine;
using System.Collections.Generic;
using ZyncopeVFXAttackOnHit;

namespace ZyncopeVFXAttackOnHitManager
{
    public class AttackOnHitManager : MonoBehaviour
    {
        
        [HideInInspector] public Transform groundAttackSpawnLoc, groundOnHitSpawnLoc, groundPierceOnHitSpawnLoc, airAttackSpawnLoc, airOnHitSpawnLoc, airPierceOnHitSpawnLoc;
        public List<GameObject> attacks = new List<GameObject>();
        [HideInInspector] public GameObject uiElement;

        public bool autoPlayOnStart;

        List<GameObject> attackVfx = new List<GameObject>();
        List<GameObject> onHitVfx = new List<GameObject>();
        GameObject currentAttackVfx, currentOnHitVfx;
        int currentActiveVFX = 0;

        void Start()
        {
            uiElement.SetActive(true);

            for (int x = 0; x < attacks.Count; x++)
            {
                GameObject vfxAttackObject = Instantiate(attacks[x], AttackSpawnLoc(attacks[x]));
                vfxAttackObject.SetActive(false);
                attackVfx.Add(vfxAttackObject);

                GameObject vfxAttackOnHitObj = Instantiate(vfxAttackObject.GetComponent<AttackOnHit>().onHitObj, AttackOnHitSpawnLoc(vfxAttackObject));
                vfxAttackOnHitObj.SetActive(false);
                onHitVfx.Add(vfxAttackOnHitObj);
            }

            currentAttackVfx = attackVfx[0];
            currentOnHitVfx = onHitVfx[0];

            if (autoPlayOnStart)
            {
                Invoke("PlayCurrentVFX", 3f);
                InvokeRepeating("NextVFX", 5.5f, 2.5f);
            }
            else
            {
                DisplayCurrentVFX();
            }

            DisplayCurrentVFXName();
        }

        Transform AttackSpawnLoc(GameObject attackType)
        {
            if (attackType.GetComponent<AttackOnHit>().onHitType == 0)
                return groundAttackSpawnLoc;
            return airAttackSpawnLoc;
        }
        Transform AttackOnHitSpawnLoc(GameObject attackType)
        {
            if (attackType.GetComponent<AttackOnHit>().onHitType == 0)
            {
                if (attackType.name.Contains("pierce"))
                    return groundPierceOnHitSpawnLoc;
                return groundOnHitSpawnLoc;
            }
            return airOnHitSpawnLoc;
        }

        public void NextVFX()
        {
            HideCurrentVFX();
            currentActiveVFX += 1;
            if (currentActiveVFX > attackVfx.Count - 1)
                currentActiveVFX = 0;
            DisplayCurrentVFX();
            DisplayCurrentVFXName();
        }

        public void PlayCurrentVFX()
        {
            DisplayCurrentVFXName();
            HideCurrentVFX();
            DisplayCurrentVFX();
        }

        public void PreviousVFX()
        {
            HideCurrentVFX();
            currentActiveVFX -= 1;
            if (currentActiveVFX < 0)
                currentActiveVFX = attackVfx.Count - 1;
            DisplayCurrentVFX();
            DisplayCurrentVFXName();
        }


        void HideCurrentVFX()
        {
            attackVfx[currentActiveVFX].SetActive(false);
            CancelInvoke("TriggerOnHitVFX");
            onHitVfx[currentActiveVFX].SetActive(false);
        }
        void DisplayCurrentVFX()
        {
            attackVfx[currentActiveVFX].SetActive(true);
            Invoke("TriggerOnHitVFX", attackVfx[currentActiveVFX].GetComponent<AttackOnHit>().durationToSpawnOnHit);
        }
        void TriggerOnHitVFX()
        {
            onHitVfx[currentActiveVFX].SetActive(true);
        }
        void DisplayCurrentVFXName()
        {
            string displayName = attackVfx[currentActiveVFX].name.Replace("zyn_pfab_", "");
            displayName = displayName.Replace("(Clone)", "");

            print(displayName);
        }
    }
}
