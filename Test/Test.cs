// Using nuget.config in the same directory as this file,
// Box3D resolves ONLY from the local artifacts directory (error if missing).
// All other packages resolve from nuget.org as usual.

using System;
using Box3D;
using static Box3D.Box3D;
#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
#endif

// https://github.com/erincatto/box3d/blob/1bec63c9ee9b8a5bb54900f201c872585ee23260/test/test_world.c#L16-L101

// This is a simple example of building and running a simulation
// using Box3D. Here we create a large ground box and a small dynamic
// box.
// There are no graphics for this example. Box3D is meant to be used
// with your rendering engine in your game engine.
static class Program
{
	public static unsafe int Main()
	{
        Console.WriteLine("Starting test...");

		// Construct a world object, which will hold and simulate the rigid bodies.
		// Note: Manually initializing to work around Mono runtime bug with delegate* fields in P/Invoke
		b3WorldDef worldDef = default;
		worldDef.gravity = new b3Vec3 { x = 0.0f, y = -10.0f, z = 0.0f };
		worldDef.enableSleep = true;
		worldDef.enableContinuous = true;
		worldDef.internalValue = 1152023;

		b3WorldId worldId = b3CreateWorld( &worldDef );
		if ( !b3World_IsValid( worldId ) )
			return 1;

		// Define the ground body.
		b3BodyDef groundBodyDef = b3DefaultBodyDef();
		groundBodyDef.position = new b3Vec3 { x = 0.0f, y = -10.0f, z = 0.0f };

		// Call the body factory which allocates memory for the ground body
		// from a pool and creates the ground box shape (also from a pool).
		// The body is also added to the world.
		b3BodyId groundId = b3CreateBody( worldId, &groundBodyDef );
		if ( !b3Body_IsValid( groundId ) )
			return 1;

		// Define the ground box shape. The extents are the half-widths of the box.
		b3BoxHull groundBox = b3MakeBoxHull( 50.0f, 10.0f, 50.0f );

		// Add the box shape to the ground body.
		b3ShapeDef groundShapeDef = b3DefaultShapeDef();
		b3CreateHullShape( groundId, &groundShapeDef, &groundBox.@base );

		// Define the dynamic body. We set its position and call the body factory.
		b3BodyDef bodyDef = b3DefaultBodyDef();
		bodyDef.type = b3BodyType.b3_dynamicBody;
		bodyDef.position = new b3Vec3 { x = 0.0f, y = 4.0f, z = 0.0f };

		b3BodyId bodyId = b3CreateBody( worldId, &bodyDef );

		// Define another box shape for our dynamic body.
		b3BoxHull dynamicBox = b3MakeCubeHull( 1.0f );

		// Define the dynamic body shape
		b3ShapeDef shapeDef = b3DefaultShapeDef();

		// Set the box density to be non-zero, so it will be dynamic.
		shapeDef.density = 1.0f;

		// Override the default friction.
		shapeDef.baseMaterial.friction = 0.3f;

		// Add the shape to the body.
		b3CreateHullShape( bodyId, &shapeDef, &dynamicBox.@base );

		// Prepare for simulation. Typically we use a time step of 1/60 of a
		// second (60Hz) and 4 sub-steps. This provides a high quality simulation
		// in most game scenarios.
		float timeStep = 1.0f / 60.0f;
		int subStepCount = 4;

		b3Vec3 position = b3Body_GetPosition( bodyId );
		b3Quat rotation = b3Body_GetRotation( bodyId );

		// This is our little game loop.
		for ( int i = 0; i < 90; ++i )
		{
			// Instruct the world to perform a single step of simulation.
			// It is generally best to keep the time step and iterations fixed.
			b3World_Step( worldId, timeStep, subStepCount );

			// Now print the position and angle of the body.
			position = b3Body_GetPosition( bodyId );
			rotation = b3Body_GetRotation( bodyId );

			// Console.WriteLine($"{position.x:F2} {position.y:F2}");
		}

		// When the world destructor is called, all bodies and joints are freed. This can
		// create orphaned ids, so be careful about your world management.
		b3DestroyWorld( worldId );

		if ( Math.Abs( position.y - 1.00f ) > 0.01f )
			return 1;
		if ( Math.Abs( rotation.v.x ) > 0.01f )
			return 1;
		if ( Math.Abs( rotation.v.z ) > 0.01f )
			return 1;

        Console.WriteLine("Test succeeded.");
		return 0;
	}
}

#if ANDROID
[Activity(Label = "Box3D Test", MainLauncher = true)]
public class MainActivity : Activity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		// Run the test on a background thread so dotnet run can attach to the process
		System.Threading.Tasks.Task.Run(() =>
		{
			try {
				int result = Program.Main();

				// Kill the process so dotnet run's pidof loop detects exit
				Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
			} catch (Exception ex) {
				Console.WriteLine($"Exception: {ex}");
				Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
			}
		});
	}
}
#endif