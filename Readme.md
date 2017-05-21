## Synopsis

C# classes for projecting points and lines from Halo 5's world coordinate system to screen space.

## Code Example

```C#

// Create a camera using the data available in Halo 5's machinima mode.
var camera = new Halo5Camera(new Vector3(X, Y, Z), Yaw, Pitch, Fov);

// Collect event data from the Halo 5 API's "Match Events" endpoint.

// Project event data to screen coordinates
var projectedPoint = camera.Project(event.KillerWorldLocation);

var projectedLine = camera.Project(event.KillerWorldLocation, event.VictimWorldLocation);

// Check the visibility of the result
if (line.Visibility == ProjectedLine.LineVisibility.Clipped)
  return;

```
## Motivation

This was created for an API hackathon run by 343 Industries. It is used to project event data for www.HaloTheater.com.

[Halo APIs](developer.haloapi.com)

## Installation

The classed require Matrix, Vector and floating point math classes. Feel free to use your own or copy mine from my MathLib repository.
