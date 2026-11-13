using UnityEngine;

namespace ChessFight.ChessBoxingPrototype
{
    // Local opponent: observes distance and visible windups, never reads player inputs.
    public sealed class CBBrain
    {
        readonly CBFighter self, enemy;
        readonly CBSettings settings;
        readonly System.Random random = new System.Random(521);
        float nextAttack, guardLeft, noticed, circleLeft;
        bool sawWindup, answerWindup, nextLeft = true;
        Vector2 guardIntent = Vector2.up;
        int circleSign = 1;

        public CBBrain(CBFighter fighter, CBFighter target, CBSettings config)
        { self = fighter; enemy = target; settings = config; }

        public void Reset()
        { nextAttack = 0.65f; guardLeft = noticed = circleLeft = 0f; sawWindup = false; }

        public void Tick(float dt)
        {
            nextAttack -= dt;
            guardLeft -= dt;
            circleLeft -= dt;
            Vector3 toward = enemy.Position - self.Position;
            toward.y = 0f;
            float distance = toward.magnitude;
            Vector3 forward = distance > 0.001f ? toward / distance : Vector3.forward;
            if (enemy.WindingUp && !sawWindup)
            {
                sawWindup = true;
                noticed = 0f;
                answerWindup = random.NextDouble() < settings.aiGuardChance;
                // The hand and windup are already visible; player mouse input is never inspected.
                guardIntent = enemy.AttackLow || enemy.AttackUppercut ? Vector2.down
                    : new Vector2(enemy.AttackLeft ? .75f : -.75f, .3f);
            }
            if (sawWindup)
            {
                noticed += dt;
                if (noticed >= settings.aiReaction && answerWindup)
                { guardLeft = Mathf.Max(guardLeft, 0.35f); answerWindup = false; }
                if (!enemy.Attacking) sawWindup = false;
            }
            if (circleLeft <= 0f)
            {
                circleSign = random.Next(2) == 0 ? -1 : 1;
                circleLeft = 1f + (float)random.NextDouble();
            }
            Vector3 movement = Vector3.zero;
            bool tired = self.Stamina < 25f;
            if (enemy.State == CBBodyState.Standing)
            {
                if (distance > (tired ? 1.6f : 0.92f)) movement = forward;
                else if (distance < (tired ? 1.25f : 0.78f)) movement = -forward;
                if (distance < 1.4f) movement += Vector3.Cross(Vector3.up, forward) * (circleSign * 0.28f);
            }
            else if (distance < 1.4f) movement = -forward * 0.5f;
            if (!self.Attacking && guardLeft <= 0f && nextAttack <= 0f && distance < 1.06f && !tired
                && enemy.State == CBBodyState.Standing)
            {
                if (random.NextDouble() < 0.2)
                    guardLeft = 0.45f + (float)random.NextDouble() * 0.35f;
                else
                {
                    bool low = random.NextDouble() < .35;
                    bool rising = !low && distance < .95f && random.NextDouble() < .25;
                    self.SetIntent(new Vector2(nextLeft ? -.5f : .5f, low ? -.7f : .4f));
                    if (self.PunchSelected(nextLeft, low, rising)) nextLeft = !nextLeft;
                }
                nextAttack = 0.95f + (float)random.NextDouble() * 0.55f;
            }
            if (guardLeft > 0f) self.SetIntent(guardIntent);
            self.Command(movement, guardLeft > 0f && self.Stamina > 18f);
        }
    }
}
