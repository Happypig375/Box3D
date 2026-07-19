// Using nuget.config in the same directory as this file,
// Box3D resolves ONLY from the local artifacts directory (error if missing).
// All other packages resolve from nuget.org as usual.

using System;
using System.Runtime.InteropServices;
using Box3D;
using static Box3D.Box3D;

// https://github.com/erincatto/box3d/blob/1bec63c9ee9b8a5bb54900f201c872585ee23260/test/test_world.c#L16-L101

// This is a simple example of building and running a simulation
// using Box3D. Here we create a large ground box and a small dynamic
// box.
// There are no graphics for this example. Box3D is meant to be used
// with your rendering engine in your game engine.
static class Program
{
	private static int s_assertFiredCount;

	[UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
	private static unsafe int AssertCallback(sbyte* condition, sbyte* fileName, int lineNumber)
	{
		s_assertFiredCount++;
		string condStr = Marshal.PtrToStringUTF8((nint)condition) ?? "(null)";
		string fileStr = Marshal.PtrToStringUTF8((nint)fileName) ?? "(null)";
		Console.WriteLine($"Native assert [{s_assertFiredCount}]: {condStr} at {fileStr}:{lineNumber}");
		return 0; // prevent native __debugbreak()/__builtin_trap()
	}

	public static unsafe int Main()
	{
        Console.WriteLine("Starting test...");

		// Construct a world object, which will hold and simulate the rigid bodies.
		b3WorldDef worldDef = b3DefaultWorldDef();
		worldDef.gravity = new b3Vec3 { x = 0.0f, y = -10.0f, z = 0.0f };

#if LARGE_WORLDS
		b3WorldId worldId = b3CreateWorldDoublePrecision( &worldDef );
#else
		b3WorldId worldId = b3CreateWorld( &worldDef );
#endif
		if ( !b3World_IsValid( worldId ) )
			return 3001;

		// Define the ground body.
		b3BodyDef groundBodyDef = b3DefaultBodyDef();
#if LARGE_WORLDS
		groundBodyDef.position = new b3Pos { x = 0.0, y = -10.0, z = 0.0 };
#else
		groundBodyDef.position = new b3Vec3 { x = 0.0f, y = -10.0f, z = 0.0f };
#endif

		// Call the body factory which allocates memory for the ground body
		// from a pool and creates the ground box shape (also from a pool).
		// The body is also added to the world.
		b3BodyId groundId = b3CreateBody( worldId, &groundBodyDef );
		if ( !b3Body_IsValid( groundId ) )
			return 3002;

		// Define the ground box shape. The extents are the half-widths of the box.
		b3BoxHull groundBox = b3MakeBoxHull( 50.0f, 10.0f, 50.0f );

		// Add the box shape to the ground body.
		b3ShapeDef groundShapeDef = b3DefaultShapeDef();
		b3CreateHullShape( groundId, &groundShapeDef, &groundBox.@base );

		// Define the dynamic body. We set its position and call the body factory.
		b3BodyDef bodyDef = b3DefaultBodyDef();
		bodyDef.type = b3BodyType.b3_dynamicBody;
#if LARGE_WORLDS
		bodyDef.position = new b3Pos { x = 0.0, y = 4.0, z = 0.0 };
#else
		bodyDef.position = new b3Vec3 { x = 0.0f, y = 4.0f, z = 0.0f };
#endif

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

#if LARGE_WORLDS
		b3Pos position = b3Body_GetPosition( bodyId );
#else
		b3Vec3 position = b3Body_GetPosition( bodyId );
#endif
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

#if LARGE_WORLDS
		if ( Math.Abs( position.y - 1.00 ) > 0.01 )
#else
		if ( Math.Abs( position.y - 1.00f ) > 0.01f )
#endif
			return 3003;
		if ( Math.Abs( rotation.v.x ) > 0.01f )
			return 3004;
		if ( Math.Abs( rotation.v.z ) > 0.01f )
			return 3005;

		// === Validate correct native binary is loaded ===
		// Register assert callback first.
		b3SetAssertFcn(&AssertCallback);

		// Trigger a controlled assert: b3SetAssertFcn(null) calls
		// B3_ASSERT(assertFcn != NULL) inside the library.
		// In Debug native binary, this calls our callback.
		// In Release native binary, B3_ASSERT is stripped.
		s_assertFiredCount = 0;
		b3SetAssertFcn(null);
		b3SetAssertFcn(&AssertCallback); // re-register the real callback

#if DEBUG
		if (s_assertFiredCount != 1)
		{
			Console.Error.WriteLine($"ERROR: Expected native assert to fire once in Debug build but {s_assertFiredCount} fired");
			return 3101;
		}
		Console.WriteLine($"Debug native binary verified: one assert fired");
#else
		if (s_assertFiredCount > 0)
		{
			Console.Error.WriteLine("ERROR: Unexpected assert in Release build");
			return 3102;
		}
		Console.WriteLine("Release native binary verified: asserts stripped");
#endif

		// === Validate that the other configuration binary is unreachable ===
		#if DEBUG
		var suffix = "";
		#else
		var suffix = "d";
		#endif
		var libName =
			OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst()
			? $"@rpath/box3d{suffix}.framework/box3d{suffix}"
			: $"box3d{suffix}";
		if (NativeLibrary.TryLoad(libName, out IntPtr handle))
		    return 3103; // Box3D.targets has a bug if this happens

        Console.WriteLine("Test succeeded.");
		return 0;
	}
}

#if ANDROID
[Android.App.Activity(Label = "Box3D Test", MainLauncher = true)]
public class MainActivity : Android.App.Activity
{
	protected override async void OnCreate(Android.OS.Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		await System.Threading.Tasks.Task.Delay(6000); // Wait for dotnet run to detect process ID
		Java.Lang.JavaSystem.Exit(Program.Main());
	}
}
#endif