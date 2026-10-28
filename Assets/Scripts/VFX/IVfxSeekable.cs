namespace ChessFight.VFX
{
    /// <summary>
    /// A part of an effect that can be put straight into the state it has <c>age</c> seconds after it was turned on
    /// (R172, 10-27, the capture tool and scrubbing): <see cref="VfxCue.Seek"/> calls it on every part under the cue, so
    /// the same t always gives the same frame. A part drives everything it draws from that age (scale, flash, fade and
    /// the shader's _VfxTime), never from Time.time or the shader's _Time.
    /// </summary>
    public interface IVfxSeekable
    {
        void Seek(float age);
    }
}
