// SEPARATE from soulMode. Two distinct reasons to block movement:
//   soulMode  = movement blocked because soul is out (teleport system)
//   frozen    = movement blocked because trap grabbed this player
// Keeping them separate means trap release doesn't accidentally re-enable
// soul movement, and soul-out doesn't accidentally unfreeze a trapped player.
// ───────────────────────────────────────────────────────────────────────
public interface IMovementFreezable
{
    void SetFrozen(bool frozen);
}
