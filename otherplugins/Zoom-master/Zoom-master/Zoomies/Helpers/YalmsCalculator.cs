using System;
using System.Numerics;

namespace ZoomiesPlugin.Helpers
{
    public class YalmsCalculator
    {
        // State for calculating speed
        private Vector3 previousPosition;
        private DateTime previousTime;
        private float currentYalms;
        private float displayYalms;
        private float damping;

        public YalmsCalculator()
        {
            previousPosition = Vector3.Zero;
            previousTime = DateTime.Now;
            currentYalms = 0.0f;
            displayYalms = 0.0f;
            damping = 0.1f; // Lower values create smoother needle movement
        }

        public float GetDisplayYalms()
        {
            return displayYalms;
        }

        public float GetCurrentYalms()
        {
            return currentYalms;
        }

        public Vector3 GetPreviousPosition()
        {
            return previousPosition;
        }

        public DateTime GetPreviousTime()
        {
            return previousTime;
        }

        public void SetDamping(float newDamping)
        {
            damping = Math.Clamp(newDamping, 0.01f, 1.0f);
        }

        public void Update(Vector3 currentPosition)
        {
            // Initialize position data on first call
            if (previousPosition == Vector3.Zero)
            {
                previousPosition = currentPosition;
                previousTime = DateTime.Now;
                return;
            }

            DateTime currentTime = DateTime.Now;
            double deltaTime = (currentTime - previousTime).TotalSeconds;

            // Only update if we have a reasonable time difference
            if (deltaTime > 0.01)
            {
                // Only measure horizontal movement (X/Z axes)
                float distanceTraveled = new Vector2(
                    currentPosition.X - previousPosition.X,
                    currentPosition.Z - previousPosition.Z
                ).Length();

                currentYalms = distanceTraveled / (float)deltaTime;
                previousPosition = currentPosition;
                previousTime = currentTime;
            }

            // Apply damping for smooth animation
            displayYalms = displayYalms + (currentYalms - displayYalms) * damping;
        }

        public void Reset()
        {
            currentYalms = 0.0f;
            displayYalms = 0.0f;
            previousPosition = Vector3.Zero;
            previousTime = DateTime.Now;
        }
    }
}
