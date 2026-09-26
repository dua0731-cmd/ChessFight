namespace ChessFight.Gameplay
{
    // Something a character works with the interact key: a pioneer bell, later a
    // lever. When the key goes down the character finds the nearest one in reach
    // and hands over who it is and its team (Teams). Only the machine that
    // simulates the character calls it.
    public interface IInteractable
    {
        // True when it did something.
        bool Interact(ICharacterDriver who, int team);
    }
}
