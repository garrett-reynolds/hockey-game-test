using System.Linq;
using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The roof lights in the Goal Show and the new-best light show: the overhead light banks and the spots along the skate-in
    /// take the Arena Show's pulse colour while its flash is Lit, and are their normal white the rest of the time. The crease
    /// lights (the goalie's and net's shadows) and the main light never pulse. Out of a show the lights are exactly as
    /// ArenaLighting built them, so the Calm Area before the Shot never sees a pulse.
    /// </summary>
    public sealed class RoofLights
    {
        static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");

        readonly Material _banks;     // one material shared by every light bank
        readonly Color _bankGlow;
        readonly Light[] _spots;
        readonly Color[] _spotColours;
        bool _pulsing;
        ArenaColour _colour;

        /// <summary>Finds the roof lights ArenaLighting.Build put under rink.</summary>
        public RoofLights(Transform rink)
        {
            _banks = rink.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == ArenaLighting.LightBank)?.sharedMaterial;
            if (_banks != null) _bankGlow = _banks.GetColor(EmissionColour);
            _spots = rink.GetComponentsInChildren<Light>(true).Where(l => l.name == ArenaLighting.OverheadLight).ToArray();
            _spotColours = _spots.Select(l => l.color).ToArray();
        }

        /// <summary>Follows the Arena Show's Goal Show lights: changes the lights only when the flash turns on or off.</summary>
        public void Show(GoalShowLights lights)
        {
            bool pulsing = lights.RoofPulse && lights.Lit;
            if (pulsing == _pulsing && (!pulsing || lights.PulseColour.Equals(_colour))) return;
            _pulsing = pulsing;
            _colour = lights.PulseColour;

            Color pulse = RibbonBoards.ColourOf(lights.PulseColour);
            if (_banks != null) _banks.SetColor(EmissionColour, pulsing ? pulse * ArenaLighting.BankGlow : _bankGlow);
            for (int i = 0; i < _spots.Length; i++)
                _spots[i].color = pulsing ? pulse : _spotColours[i];
        }
    }
}
