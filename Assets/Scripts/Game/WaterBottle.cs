using System;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The water bottle on top of the net (#163): a green Gatorade-style squeeze bottle, real size (20.7 cm long with its short cap, 7.5 cm wide), lying on its
    /// side across the net's top, near the back and toward the middle, like a real goalie's. The model is Assets/Resources/Models/bottle.obj (Tools/make_water_bottle.py),
    /// or a green cylinder when there is none. It has a Rigidbody and one box collider, and it lies on the catch roof (ShotColliders),
    /// the net's top for physics. While it sits it is kinematic, so it never drifts; the knock-off (#164) lets it go.
    /// While it sits it never meets the puck (ADR 0004: Unity physics decides the Shot): the bottle and the physics puck are on layers that do not
    /// collide (IgnorePuck), so the bottle cannot change a Shot's result. Once knocked it is on KnockedLayer and meets everything, the puck too:
    /// the Goal is decided before the knock.
    /// </summary>
    public sealed class WaterBottle
    {
        public const string ObjectName = "Water Bottle";
        /// <summary>The body's picture in Assets/Resources/Models (no extension).</summary>
        public const string BodyPicture = "bottle-body";
        /// <summary>The bottle's physics layer, and the puck's (PhysicsPuck.Layer): the layer rule ignores collisions between them. Unnamed layers.</summary>
        public const int Layer = 28;

        public const float Height = 0.207f, Width = 0.075f;
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
            PlaceLook(root.transform, findFile);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, Height / 2f, 0f);
            box.size = new Vector3(Width, Height, Width);
            box.sharedMaterial = Material;
            var body = root.AddComponent<Rigidbody>();
            body.mass = Mass;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            SetLayer(root.transform, Layer);
            var made = new WaterBottle { Root = root, Body = body, RestPosition = root.transform.position, RestRotation = root.transform.rotation };
            root.AddComponent<Link>().Bottle = made;
            return made;
        }

        /// <summary>The bottle's picture under root: the model (or the placeholder), with the body's picture on a copy of its material.</summary>
        static void PlaceLook(Transform root, Func<string, GameObject> findFile)
        {
            GameObject model = ModelFiles.Place(root, ModelFiles.WaterBottle, PlaceholderBottle, findFile, null);
            // The body's picture (the swoosh and the G with the bolt, Tools/make_water_bottle.py), on a copy of the body's material.
            Texture picture = Resources.Load<Texture2D>(ModelFiles.Folder + "/" + BodyPicture);
            foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
            {
                if (part.name != "Body" || picture == null) continue;
                var material = new Material(part.sharedMaterial) { mainTexture = picture, color = Color.white };
                part.sharedMaterial = material;
            }
        }

        /// <summary>
        /// A bottle only to look at, under parent (the Replay's stand-in, ReplayView.ReplayBottle): drawn as the bottle is, with no
        /// Rigidbody and no hit shapes, so nothing in physics meets it.
        /// </summary>
        public static GameObject MakeLook(Transform parent, Func<string, GameObject> findFile = null)
        {
            var look = new GameObject("Replay Water Bottle");
            look.transform.SetParent(parent, false);
            PlaceLook(look.transform, findFile);
            foreach (Collider hit in look.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(hit); // the placeholder cylinder comes with one
            return look;
        }

        /// <summary>Lets the Round driver find the bottle the rink built.</summary>
        sealed class Link : MonoBehaviour { public WaterBottle Bottle; }

        /// <summary>The bottle built under this rink, or null.</summary>
        public static WaterBottle In(Transform rink)
        {
            Link link = rink.GetComponentInChildren<Link>();
            return link != null ? link.Bottle : null;
        }

        /// <summary>
        /// The push ranges (#164, m/s): up, forward (toward the shooter, -z, out over the crossbar) and sideways (left or right, picked
        /// each knock). Up 2.2 to 3.0 lifts it 0.25 to 0.46 m and keeps it in the air about 0.8 s from the net's top (1.4 m), so forward
        /// 2.0 to 3.5 clears the crossbar (z 0, 0.28 m ahead of it) and lands it 1.6 to 3 m in front of the goal line, on the ice.
        /// </summary>
        public const float PushUpMin = 2.2f, PushUpMax = 3.0f, PushForwardMin = 2.0f, PushForwardMax = 3.5f, PushSideMin = 0.8f, PushSideMax = 2.0f;
        /// <summary>The most spin a knock gives about each axis (rad/s).</summary>
        public const float SpinMax = 6f;

        /// <summary>The layer a knocked bottle is on: Default, which meets everything, the puck too (the Goal is decided before the knock).</summary>
        public const int KnockedLayer = 0;

        /// <summary>True once Knock has let it go, until PutBack.</summary>
        public bool Knocked => Body != null && !Body.isKinematic;

        /// <summary>
        /// The bottle gets its push (#164): it becomes a normal rigid body, forward, up and to a random side with a random size and a
        /// little spin, so it leaves the net, then bounces and rolls on the ice. It moves to a layer that meets the puck and everything
        /// else (PutBack puts it on its own layer again). A second Knock while it is already knocked does nothing.
        /// </summary>
        public void Knock() => Knock(new System.Random());

        /// <summary>Knock with a given random source (tests pass a seeded one).</summary>
        public void Knock(System.Random random) => Knock(random, 1f);

        /// <summary>
        /// Knock with a jump scale (#164, the Dev setting "Bottle jump" as a fraction: 1 = the push above, 2 = twice as hard). It scales
        /// the whole random push (up, forward, sideways) and the spin by the same factor.
        /// </summary>
        public void Knock(System.Random random, float jumpScale)
        {
            if (Knocked) return;
            float Between(float low, float high) => low + (float)random.NextDouble() * (high - low);
            float side = Between(PushSideMin, PushSideMax) * (random.Next(2) == 0 ? -1f : 1f);
            SetLayer(Root.transform, KnockedLayer);
            Body.isKinematic = false;
            Body.linearVelocity = new Vector3(side, Between(PushUpMin, PushUpMax), -Between(PushForwardMin, PushForwardMax)) * jumpScale;
            Body.angularVelocity = new Vector3(Between(-SpinMax, SpinMax), Between(-SpinMax, SpinMax), Between(-SpinMax, SpinMax)) * jumpScale;
        }

        /// <summary>Once a frame with BottleKnock.Knocked (the Arena Show's state): knocked off while it is true, back on the net when it turns false.</summary>
        public void Follow(bool knocked, float jumpScale = 1f)
        {
            if (knocked) Knock(new System.Random(), jumpScale);
            else if (Knocked) PutBack();
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
            SetLayer(Root.transform, Layer);
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
