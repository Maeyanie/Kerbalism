﻿using KSP.Localization;

namespace KERBALISM
{

	public sealed class GravityRing : PartModule, ISpecifics
	{
		// config
		[KSPField] public double ec_rate;                                  // ec consumed per-second when deployed
		[KSPField] public string deploy = string.Empty;                    // a deploy animation can be specified
		[KSPField] public string rotate = string.Empty;                    // a rotate loop animation can be specified

		// persistence
		[KSPField(isPersistant = true)] public bool deployed;              // true if deployed

		// Add compatibility and revert animation
		[KSPField] public bool  animBackwards;                             // If animation is playing in backward, this can help to fix
		[KSPField] public bool  rotateIsTransform;                         // Rotation is not an animation, but a Transform
		[KSPField] public float SpinRate = 20.0f;                          // Speed of the centrifuge rotation in deg/s
		[KSPField] public float SpinAccelerationRate = 1.0f;               // Rate at which the SpinRate accelerates (deg/s/s)

		private bool waitRotation;

		// animations
		Animator deploy_anim;
		public Animator rotate_anim;

		// Add compatibility
		public Transformator rotate_transf;

		// pseudo-ctor
		public override void OnStart(StartState state)
		{
			// don't break tutorial scenarios
			if (Lib.DisableScenario(this)) return;

			// get animations
			deploy_anim = new Animator(part, deploy);
			rotate_anim = new Animator(part, rotate);

			// if is using Transform
			rotate_transf = new Transformator(part, rotate, SpinRate, SpinAccelerationRate);

			// set animation state / invert animation
			deploy_anim.still(deployed ? 1.0f : 0.0f);
			deploy_anim.stop();

			if (deployed)
			{
				rotate_transf.Play();
				rotate_anim.play(false, true);
			}

			// show the deploy toggle if it is deployable
			Events["Toggle"].active = deploy.Length > 0;
		}

		public void Update()
		{
			// update RMB ui
			Events["Toggle"].guiName = deployed ? Localizer.Format("#KERBALISM_Generic_RETRACT") : Localizer.Format("#KERBALISM_Generic_DEPLOY");
			Events["Toggle"].active = deploy.Length > 0 && !deploy_anim.playing() && !waitRotation && ResourceCache.Info(vessel, "ElectricCharge").amount > ec_rate;
		}

		public void FixedUpdate()
		{
			// do nothing in the editor
			if (Lib.IsEditor()) return;

			// if deployed
			if (deployed || (animBackwards && !deployed))
			{
				// if there is no ec
				if (ResourceCache.Info(vessel, "ElectricCharge").amount < 0.01)
				{
					// pause rotate animation
					// - safe to pause multiple times
					if (rotateIsTransform && rotate_transf.IsRotating() && !rotate_transf.IsStopping()) rotate_transf.Stop();
					else rotate_anim.pause();
				}
				// if there is enough ec instead and is not deploying
				else if (!deploy_anim.playing())
				{
					// resume rotate animation
					// - safe to resume multiple times
					if (rotateIsTransform && (!rotate_transf.IsRotating() || rotate_transf.IsStopping())) rotate_transf.Play();
					else rotate_anim.resume(false);
				}
			}
			// stop loop animation if exist and we are retracting
			else
			{
				// Call transform.stop() if it is rotating and the Stop method wasn't called.
				if (rotateIsTransform && rotate_transf.IsRotating() && !rotate_transf.IsStopping()) rotate_transf.Stop();
				else rotate_anim.stop();
			}

			// When is not rotating
			if (waitRotation)
			{
				if (rotateIsTransform && !rotate_transf.IsRotating())
				{
					// start retract animation in the correct direction, when is not rotating
					if (animBackwards) deploy_anim.play(deployed, false);
					else deploy_anim.play(!deployed, false);
					waitRotation = false;
				}
				else if (!rotateIsTransform && !rotate_anim.playing())
				{
					if (animBackwards) deploy_anim.play(deployed, false);
					else deploy_anim.play(!deployed, false);
					waitRotation = false;
				}
			}

			// if has any animation playing, consume energy.
			if (deploy_anim.playing() || (rotate_transf.IsRotating() && !rotate_transf.IsStopping()) || rotate_anim.playing())
			{
				// get resource handler
				resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

				// consume ec
				ec.Consume(ec_rate * Kerbalism.elapsed_s);
			}

			if (rotateIsTransform && rotate_transf != null) rotate_transf.DoSpin();
		}

		public static void BackgroundUpdate(Vessel vessel, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, GravityRing ring, resource_info ec, double elapsed_s)
		{
			// if the module is either non-deployable or deployed
			if (ring.deploy.Length == 0 || Lib.Proto.GetBool(m, "deployed"))
			{
				// consume ec
				ec.Consume(ring.ec_rate * elapsed_s);
			}
		}

		[KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "#KERBALISM_GravityRing_Toggle", active = true)]
		public void Toggle()
		{
			// switch deployed state
			deployed ^= true;

			if (rotateIsTransform) waitRotation = rotate_transf.IsRotating();
			else waitRotation = rotate_anim.playing();

			if (!waitRotation)
			{
				// stop loop animation if exist and we are retracting
				if (rotateIsTransform && !rotate_transf.IsRotating())
				{
					if (animBackwards) deploy_anim.play(deployed, false);
					else deploy_anim.play(!deployed, false);
					waitRotation = false;
				}
				else if (!rotateIsTransform && !rotate_anim.playing())
				{
					if (animBackwards) deploy_anim.play(deployed, false);
					else deploy_anim.play(!deployed, false);
				}
			}
		}

		// action groups
		[KSPAction("#KERBALISM_GravityRing_Action")] public void Action(KSPActionParam param) { Toggle(); }

		// part tooltip
		public override string GetInfo()
		{
			return Specs().info();
		}

		// specifics support
		public Specifics Specs()
		{
			Specifics specs = new Specifics();
			specs.add("bonus", "firm-ground");
			specs.add("EC/s", Lib.HumanReadableRate(ec_rate));
			specs.add("deployable", deploy.Length > 0 ? "yes" : "no");
			return specs;
		}
	}

} // KERBALISM