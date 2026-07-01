using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kamgam.SandGame
{
    public class InGameMenu : MonoBehaviour
    {
        protected bool _initialized = false;
        protected MaterialButton[] _materialButtons;

        public void Init(SandGame sandGame, PixelMaterialId selectedMaterialId)
        {
            if (_initialized)
                return;

            _materialButtons = GetComponentsInChildren<MaterialButton>();
            highlightSelectedMaterialButton(selectedMaterialId);

            foreach (var btn in _materialButtons)
            {
                btn.GetComponent<Button>().onClick.AddListener(() => {
                    sandGame.DrawingMaterialId = btn.MaterialId;
                    highlightSelectedMaterialButton(btn.MaterialId);
                });
            }
        }

        protected void highlightSelectedMaterialButton(PixelMaterialId id)
        {
            foreach (var btn in _materialButtons)
            {
                btn.transform.localScale = Vector3.one * (btn.MaterialId == id ? 1.2f : 1f);
            }
        }
    }
}
