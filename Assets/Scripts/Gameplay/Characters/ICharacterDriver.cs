using UnityEngine;

namespace ChessFight.Gameplay
{
    // One tick of intent for a playable character. Deliberately the same shape as
    // RagdollPawn.PawnInput in the ragdoll lab, so its adapter is a field-by-field
    // copy.
    public struct CharacterCommand
    {
        // WORLD space, horizontal, length 0..1. Turning stick-and-camera input into
        // a world direction is the owning client's job, done before this point:
        // the host that simulates the character cannot see that client's camera.
        public Vector3 Move;
        public bool Jump;   // Edge: true for the tick the button went down.
        public bool Shove;  // Edge.
        public bool Grab;   // Held.
        public bool Sprint; // Held: run faster while stamina lasts.

        // The piece abilities (Queen of the Hill): E is every piece's main one,
        // Q the queen's and the king's second one. Edges.
        public bool Ability;
        public bool Ability2;

        // HELD, unlike the abilities. A bell or a lever reacts to the press, but en
        // passant (unhooking an enemy's grappling hook) needs it held for 0.4 s, and
        // only the held state can tell a tap from a hold. The character finds the
        // press edge itself.
        public bool Interact;

        // WORLD space unit vector the player aims along: the camera's forward, pitch
        // included. The hook is thrown along it and the charges go along it. Like
        // Move, the owning client works it out from its own camera. Zero = no aim.
        public Vector3 Aim;
    }

    // The seam between a playable character and whatever drives it.
    //
    //   offline  PlaytestSpawner feeds it the local keyboard and mouse
    //   match    the host will feed it inputs received over the network
    //   bots     BotBrain-style AI will feed it commands
    //
    // A character that reads Input itself can only ever be driven by the machine
    // it runs on, so it cannot be simulated by the host or controlled by a bot.
    // Take every intent through SetCommand, and latch the edge flags until the
    // next FixedUpdate consumes them - SetCommand is called once per frame.
    public interface ICharacterDriver
    {
        void SetCommand(in CharacterCommand command);

        // What cameras follow. A ragdoll should return its hips, not its root.
        Transform FollowTarget { get; }

        // Respawns and checkpoints. `position` is the GROUND point the feet go on,
        // not the pivot: each character knows its own height (the ragdoll lab puts
        // the hips at ground + standHeight). Must clear velocities and let go of
        // everything: whatever it holds, the wall it climbs, and anyone holding it
        // (later also hooks and ropes), so a respawn out of the water is always a
        // clean start.
        void Teleport(Vector3 position, Quaternion rotation);
    }
}
