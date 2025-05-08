using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    // Called by Animation Events in Walk/Run animations
    public void OnFootstep(AnimationEvent animationEvent)
    {
        // Debug.Log($"Footstep Triggered - Weight: {animationEvent.animatorClipInfo.weight}");
        // --- ADD FOOTSTEP SOUND/VFX LOGIC HERE LATER ---
        // Example: audioSource.PlayOneShot(footstepSound);
        // Example: Instantiate(footstepParticle, footstepPosition, Quaternion.identity);

        // animationEvent parameter contains info like which foot, animation weight etc. if needed
    }

    // Called by Animation Events in Landing animations
    public void OnLand(AnimationEvent animationEvent)
    {
        // Debug.Log("Landed!");
        // --- ADD LANDING SOUND/VFX LOGIC HERE LATER ---
        // Example: audioSource.PlayOneShot(landSound);
        // Example: Instantiate(landingParticle, transform.position, Quaternion.identity);
    }

    // You might need other methods if other events exist in animations
}