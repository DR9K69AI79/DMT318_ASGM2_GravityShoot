using UnityEngine;

namespace DWHITE
{
	[RequireComponent(typeof(Rigidbody))]
	public class CustomGravityRigidbody : MonoBehaviour
	{

		[SerializeField]
		bool floatToSleep = false;

		Rigidbody body;

		float floatDelay;

		Vector3 gravity;

		void Awake()
		{
			body = GetComponent<Rigidbody>();
			body.useGravity = false;
		}

		void FixedUpdate()
		{
			if (floatToSleep)
			{
				if (body.IsSleeping())
				{
					floatDelay = 0f;
					return;
				}

				if (body.velocity.sqrMagnitude < 0.0001f)
				{
					floatDelay += Time.deltaTime;
					if (floatDelay >= 1f)
					{
						return;
					}
				}
				else
				{
					floatDelay = 0f;
				}
			}
			gravity = CustomGravity.GetGravity(body.position);
			body.AddForce(gravity, ForceMode.Acceleration);
		}
	}
}
