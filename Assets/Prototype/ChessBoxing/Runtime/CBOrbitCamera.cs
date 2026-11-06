using UnityEngine;

namespace ChessFight.ChessBoxingPrototype
{
    // The camera consumes the same stable intent as combat; it never produces combat input.
    // All heights are relative to the arena floor, not the animated/physical head rotation.
    public sealed class CBOrbitCamera
    {
        readonly Camera view;
        readonly CBFighter player;
        readonly CBFighter opponent;
        readonly Collider playerBody;
        readonly Collider playerHead;
        readonly float floorY;
        readonly float characterHeight;
        Vector3 followPosition, lookPosition, forward = Vector3.forward;
        Vector2 shownIntent;
        float occlusionHold;
        bool initialized;

        public float BodyOpacity { get; private set; } = 1f;

        public CBOrbitCamera(Camera camera, CBFighter player, CBFighter opponent)
        {
            view = camera;
            this.player = player;
            this.opponent = opponent;
            characterHeight = player != null && player.Visual != null
                ? player.Visual.CharacterHeight : 1.05f;
            floorY = player != null && player.Body != null && player.Visual != null
                ? player.Body.position.y - player.Visual.BodyHeight : 0f;
            playerBody = player != null && player.Body != null
                ? player.Body.GetComponent<Collider>() : null;
            playerHead = player != null && player.Head != null
                ? player.Head.GetComponent<Collider>() : null;
            if (view != null)
            {
                // Keep the lens constant so camera motion does not imitate a change in reach.
                view.fieldOfView = 55f;
                view.nearClipPlane = .04f;
                view.farClipPlane = 80f;
            }
        }

        public void Tick(Vector2 intent, float dt, bool snap = false)
        {
            if (view == null || player == null || opponent == null ||
                player.Body == null || opponent.Body == null || player.Visual == null)
                return;
            if (!snap && dt <= 0f) return;

            dt = Mathf.Clamp(dt, 0f, .1f);
            snap |= !initialized;
            intent = Vector2.ClampMagnitude(intent, 1f);

            // Rendering follows the interpolated bodies, but shares the same opponent-relative yaw.
            Vector3 playerPosition = player.Body.transform.position;
            Vector3 opponentPosition = opponent.Body.transform.position;
            Vector3 toward = opponentPosition - playerPosition;
            toward.y = 0f;
            if (toward.sqrMagnitude > .01f)
            {
                toward.Normalize();
                forward = toward;
            }
            else if (!initialized)
            {
                forward = player.Facing.sqrMagnitude > .01f ? player.Facing : Vector3.forward;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            // A bounded speed avoids a large shoulder sweep on every rapid hand change.
            // This is visual easing only: the fighter receives the unsmoothed intent.
            shownIntent = snap ? intent : Vector2.MoveTowards(shownIntent, intent, 3.2f * dt);
            float up = Mathf.Max(0f, shownIntent.y);
            float down = Mathf.Max(0f, -shownIntent.y);
            float scale = characterHeight / 1.05f;
            float shoulder = shownIntent.x * .42f * (1f - down * .25f) * scale;
            float distance = (1.30f + down * .16f) * scale;
            float height = floorY + characterHeight * .98f + up * .35f * scale;
            Vector3 anchor = new Vector3(playerPosition.x, height, playerPosition.z);
            Vector3 desiredPosition = anchor - forward * distance + right * shoulder;

            // Lock onto a stable upper-body framing point. The lower stance reveals the abdomen
            // without pitching down in response to the player's own duck or knockback animation.
            float targetHeight = floorY + characterHeight * (.62f + up * .025f - down * .08f);
            Vector3 desiredLook = new Vector3(opponentPosition.x, targetHeight, opponentPosition.z);
            // No second world-space orbit lag: otherwise strafing makes the view trail the fists.
            followPosition = desiredPosition;
            lookPosition = desiredLook;
            Vector3 look = lookPosition - followPosition;
            if (look.sqrMagnitude > .001f)
                view.transform.SetPositionAndRotation(followPosition, Quaternion.LookRotation(look, Vector3.up));

            UpdateVisibility(intent, dt, snap);
            initialized = true;
        }

        void UpdateVisibility(Vector2 intent, float dt, bool snap)
        {
            float otherHeight = opponent.Visual != null ? opponent.Visual.CharacterHeight : characterHeight;
            float otherBodyHeight = opponent.Visual != null ? opponent.Visual.BodyHeight : otherHeight * .26f;
            float otherFloor = opponent.Body.position.y - otherBodyHeight;
            float headRadius = opponent.Visual != null ? opponent.Visual.HeadRadius : otherHeight * .19f;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 head = opponent.Head != null ? opponent.Head.position
                : opponent.Body.position + Vector3.up * otherHeight * .55f;
            Vector3 chest = new Vector3(opponent.Body.position.x,
                otherFloor + otherHeight * .53f, opponent.Body.position.z);
            Vector3 abdomen = new Vector3(opponent.Body.position.x,
                otherFloor + otherHeight * .36f, opponent.Body.position.z);

            bool headBlocked = BlocksView(head);
            bool abdomenBlocked = BlocksView(abdomen);
            int blocked = (headBlocked ? 1 : 0) + (abdomenBlocked ? 1 : 0);
            blocked += BlocksView(head - right * headRadius * .6f) ? 1 : 0;
            blocked += BlocksView(head + right * headRadius * .6f) ? 1 : 0;
            blocked += BlocksView(chest) ? 1 : 0;
            blocked += BlocksView(chest - right * otherHeight * .12f) ? 1 : 0;
            blocked += BlocksView(chest + right * otherHeight * .12f) ? 1 : 0;

            bool close = CameraInside(playerBody) || CameraInside(playerHead);
            if (player.Head != null && player.Visual != null)
                close |= Vector3.Distance(view.transform.position, player.Head.position)
                    < player.Visual.HeadRadius + .10f;
            bool obstructing = close || blocked >= 2 ||
                (intent.y < -.25f && abdomenBlocked) || (intent.y > .25f && headBlocked);
            if (obstructing) occlusionHold = .18f;
            else if (snap) occlusionHold = 0f;
            else occlusionHold = Mathf.Max(0f, occlusionHold - dt);

            float goal = obstructing || occlusionHold > 0f ? .16f : 1f;
            if (snap) BodyOpacity = goal;
            else BodyOpacity = Mathf.Lerp(BodyOpacity, goal, Damp(goal < BodyOpacity ? 18f : 7f, dt));
            if (Mathf.Abs(BodyOpacity - goal) < .002f) BodyOpacity = goal;
            player.Visual.SetBodyOpacity(BodyOpacity);
        }

        bool BlocksView(Vector3 target)
        {
            Vector3 onScreen = view.WorldToViewportPoint(target);
            if (onScreen.z <= view.nearClipPlane || onScreen.x < 0f || onScreen.x > 1f ||
                onScreen.y < 0f || onScreen.y > 1f) return false;
            Vector3 line = target - view.transform.position;
            float distance = line.magnitude;
            if (distance <= .06f) return false;
            Ray ray = new Ray(view.transform.position, line / distance);
            // Only this player's body/head can request a body fade. Gloves and ring objects
            // never trigger it, and their real occlusion remains visible.
            return HitsBeforeTarget(playerBody, ray, distance - .04f) ||
                HitsBeforeTarget(playerHead, ray, distance - .04f);
        }

        static bool HitsBeforeTarget(Collider collider, Ray ray, float distance)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
                collider.Raycast(ray, out RaycastHit hit, distance);
        }

        bool CameraInside(Collider collider)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy &&
                (collider.ClosestPoint(view.transform.position) - view.transform.position).sqrMagnitude < .000001f;
        }

        static float Damp(float speed, float dt) => 1f - Mathf.Exp(-speed * dt);
    }
}
