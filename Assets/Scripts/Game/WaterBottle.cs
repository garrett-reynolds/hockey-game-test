using System;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The water bottle on top of the net (#163): a green Gatorade-style squeeze bottle, real size (25 cm long, 7.5 cm wide), lying on its
    /// side across the net's top, near the back and toward the middle, like a real goalie's. The model is Assets/Resources/Models/bottle.obj (Tools/make_water_bottle.py),
    /// or a green cylinder when there is none. It has a Rigidbody and one box collider, and it lies on the catch roof (ShotColliders),
    /// the net's top for physics. While it sits it is kinematic, so it never drifts; the knock-off (#164) lets it go.
    /// It never meets the puck (ADR 0004: Unity physics decides the Shot): the bottle and the physics puck are on layers that do not
    /// collide (IgnorePuck), so the bottle cannot change a Shot's result.
    /// </summary>
    public sealed class WaterBottle
    {
        public const string ObjectName = "Water Bottle";
        /// <summary>The body's picture in Assets/Resources/Models (no extension).</summary>
        public const string BodyPicture = "bottle-body";
        /// <summary>The bottle's physics layer, and the puck's (PhysicsPuck.Layer): the layer rule ignores collisions between them. Unnamed layers.</summary>
        public const int Layer = 28;

        public const float Height = 0.25f, Width = 0.075f;
        /// <summary>A full bottle (kg).</summary>
        public const float Mass = 0.8f;
        /// <summary>
        /// Where it stands: x to one side of the goal light (which hangs over the middle). z is the back of the net's flat top: the net
        /// model's top frame and pad run flat from the crossbar to about 0.37 m behind the Net Face, then the netting slopes down to the
        /// back (a bottle further back would hang in the air over the slope).
        /// </summary>
        const float Z = 0.28f;
        /// <summary>
        /// The bottle lies on its side (Garrett, #163), its long axis across the net (along the crossbar), the cap toward the middle. Its
        /// middle is CentreX to one side of the goal light, which hangs over the net's middle. The model's own axis runs up (Y), so the
        /// root is turned 90 degrees about Z: the bottle's base is then at the root and it reaches toward -X.
        /// </summary>
        const float CentreX = 0.35f;
        static readonly Quaternion Lying = Quaternion.Euler(0f, 0f, 90f);
        /// <summary>The top of the catch roof, which the bottle stands on.</summary>
        const float BaseY = (float)NetFaceMapping.HeightMetres + ShotColliders.CatchGap + ShotColliders.CatchSideThickness;

        static PhysicsMaterial _material;
        /// <summary>Plastic: a little bounce, a good grip. Made once and kept.</summary>
        static PhysicsMaterial Material => _material != null ? _material : _material = new PhysicsMaterial("Water Bottle")
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset,
            bounciness = 0.3f, dynamicFriction = 0.4f, staticFriction = 0.5f,
        };

        public GameObject Root { get; private set; }
        public Rigidbody Body { get; private set; }
        /// <summary>Where the bottle sits on the net (world space).</summary>
        public Vector3 RestPosition { get; private set; }
        /// <summary>How the bottle lies on the net (world space).</summary>
        public Quaternion RestRotation { get; private set; }

        /// <summary>A new bottle under the rink, lying on the net. findFile is for tests; see ModelFiles.Load.</summary>
        public static WaterBottle Build(Transform rink, Func<string, GameObject> findFile = null)
        {
            IgnorePuck();
            var root = new GameObject(ObjectName);
            root.transform.SetParent(rink, false);
            root.transform.localPosition = new Vector3(CentreX + Height / 2f, BaseY + Width / 2f, Z);
            root.transform.localRotation = Lying;
            GameObject model = ModelFiles.Place(root.transform, ModelFiles.WaterBottle, PlaceholderBottle, findFile, null);
            // The body's picture (the swoosh and the G with the bolt, Tools/make_water_bottle.py), on a copy of the body's material.
            Texture picture = Resources.Load<Texture2D>(ModelFiles.Folder + "/" + BodyPicture);
            foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
            {
                if (part.name != "Body" || picture == null) continue;
                var material = new Material(part.sharedMaterial) { mainTexture = picture, color = Color.white };
                part.sharedMaterial = material;
            }

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, Height / 2f, 0f);
            box.size = new Vector3(Width, Height, Width);
            box.sharedMaterial = Material;
            var body = root.AddComponent<Rigidbody>();
            body.mass = Mass;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            SetLayer(root.transform, Layer);
            return new WaterBottle { Root = root, Body = body, RestPosition = root.transform.position, RestRotation = root.transform.rotation };
        }

        /// <summary>The one fixed push (#164): the speed it is given, up and back away from the net (+z, behind the goal line). Always the same.</summary>
        public static readonly Vector3 KnockSpeed = new Vector3(0f, 2.5f, 3f);
        /// <summary>True once Knock has let it go, until PutBack.</summary>
        public bool Knocked => Body != null && !Body.isKinematic;

        /// <summary>
        /// The bottle gets its push (#164): it becomes a normal rigid body and is given KnockSpeed, so it leaves the net, then bounces
        /// and rolls on the ice. A second Knock while it is already knocked does nothing.
        /// </summary>
        public void Knock()
        {
            if (Knocked) return;
            Body.isKinematic = false;
            Body.linearVelocity = KnockSpeed;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>Back on the net, lying as it did, still, held there again.</summary>
        public void PutBack()
        {
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
                Body.isKinematic = true;
            }
            Body.position = RestPosition;
            Body.rotation = RestRotation;
            Root.transform.SetPositionAndRotation(RestPosition, RestRotation);
        }

        /// <summary>
        /// The layer rule: nothing on the bottle's layer meets anything on the puck's layer. Every other pair is untouched, so the bottle
        /// still rests on the net and lands on the ice, and the puck still meets the ice, boards, posts, net and goalie. The collision
        /// matrix is a Physics setting: this sets it from code each time a bottle or a puck is built, so no ProjectSettings file changes.
        /// </summary>
        public static void IgnorePuck() => Physics.IgnoreLayerCollision(Layer, PhysicsPuck.Layer, true);

        internal static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }

        /// <summary>A plain green cylinder, used when Assets/Resources/Models has no bottle model.</summary>
        static GameObject PlaceholderBottle()
        {
            var bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottle.name = "Bottle Placeholder";
            bottle.transform.localPosition = new Vector3(0f, Height / 2f, 0f);
            bottle.transform.localScale = new Vector3(Width, Height / 2f, Width);
            RinkBuilder.Paint(bottle, new Color(0.05f, 0.62f, 0.18f));
            return bottle;
        }
    }
}
