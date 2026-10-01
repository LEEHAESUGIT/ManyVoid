////using Unity.Entities;
////using Unity.Transforms;
////using Unity.Mathematics;
////using Unity.Collections;
////using UnityEngine;


//using Unity.Burst;
//using Unity.Collections;
//using Unity.Entities;
//using Unity.Mathematics;
//using Unity.Transforms;
//using UnityEngine;

//[BurstCompile]
//public partial class BoidSystemECS : SystemBase
//{
//	private BoidControllerECS controller;

//	protected override void OnUpdate()
//	{
//		if (controller == null)
//		{
//			controller = BoidControllerECS.Instance;
//		}

//		if (controller == null)
//			return;

//		EntityQuery query = GetEntityQuery(
//			ComponentType.ReadOnly<BoidECS>(),
//			ComponentType.ReadWrite<LocalTransform>()
//		);

//		NativeArray<LocalTransform> transforms =
//			query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

//		NativeArray<LocalTransform> newTransforms =
//			new NativeArray<LocalTransform>(transforms.Length, Allocator.Temp);

//		for (int i = 0; i < transforms.Length; i++)
//		{
//			float3 boidPosition = transforms[i].Position;
//			float3 boidForward =
//				math.forward(transforms[i].Rotation);

//			float3 separationSum = float3.zero;
//			float3 positionSum = float3.zero;
//			float3 headingSum = float3.zero;

//			int boidsNearby = 0;

//			for (int j = 0; j < transforms.Length; j++)
//			{
//				if (i == j)
//					continue;

//				float3 otherPosition = transforms[j].Position;

//				float dist =
//					math.distance(boidPosition, otherPosition);

//				if (dist < controller.boidPerceptionRadius)
//				{
//					separationSum +=
//						-(otherPosition - boidPosition)
//						* (1f / math.max(dist, 0.0001f));

//					positionSum += otherPosition;

//					headingSum +=
//						math.forward(transforms[j].Rotation);

//					boidsNearby++;
//				}
//			}

//			float3 force = float3.zero;

//			if (boidsNearby > 0)
//			{
//				force +=
//					(separationSum / boidsNearby)
//					* controller.separationWeight;

//				force +=
//					((positionSum / boidsNearby) - boidPosition)
//					* controller.cohesionWeight;

//				force +=
//					(headingSum / boidsNearby)
//					* controller.alignmentWeight;
//			}

//			float halfCage = controller.cageSize * 0.5f;

//			if (math.min(math.min(
//				halfCage - math.abs(boidPosition.x),
//				halfCage - math.abs(boidPosition.y)),
//				halfCage - math.abs(boidPosition.z))
//				< controller.avoidWallsTurnDist)
//			{
//				force +=
//					-math.normalize(boidPosition)
//					* controller.avoidWallsWeight;
//			}

//			float3 velocity =
//				boidForward * controller.boidSpeed;

//			velocity += force * SystemAPI.Time.DeltaTime;

//			velocity =
//				math.normalize(velocity)
//				* controller.boidSpeed;

//			LocalTransform newTransform = transforms[i];

//			newTransform.Position +=
//				velocity * SystemAPI.Time.DeltaTime;

//			newTransform.Rotation =
//				quaternion.LookRotationSafe(
//					velocity,
//					math.up()
//				);

//			newTransforms[i] = newTransform;
//		}

//		int index = 0;

//		Entities
//			.WithAll<BoidECS>()
//			.ForEach((ref LocalTransform transform) =>
//			{
//				transform = newTransforms[index];
//				index++;
//			})
//			.Run();

//		transforms.Dispose();
//		newTransforms.Dispose();
//	}
//}

////public class BoidSystemECS : ComponentSystem {

////    private BoidControllerECS controller;

////    protected override void OnUpdate() {

////        // This runs only if there exists a BoidControllerECS instance.
////        if (!controller) {
////            controller = BoidControllerECS.Instance;
////        }
////        if (controller) {
////            EntityQuery boidQuery = GetEntityQuery(ComponentType.ReadOnly<BoidECS>(), ComponentType.ReadOnly<LocalToWorld>());
////            NativeArray<float4x4> newBoidPositions = new NativeArray<float4x4>(boidQuery.CalculateEntityCount(), Allocator.Temp);

////            int boidIndex = 0;
////            Entities.WithAll<BoidECS>().ForEach((Entity boid, ref LocalToWorld localToWorld) => {
////                float3 boidPosition = localToWorld.Position;
                
////                float3 seperationSum = float3.zero;
////                float3 positionSum = float3.zero;
////                float3 headingSum = float3.zero;

////                int boidsNearby = 0;
                
////                Entities.WithAll<BoidECS>().ForEach((Entity otherBoid, ref LocalToWorld otherLocalToWorld) => {
////                    if (boid != otherBoid) {
                        
////                        float distToOtherBoid = math.length(boidPosition - otherLocalToWorld.Position);
////                        if (distToOtherBoid < controller.boidPerceptionRadius) {

////                            seperationSum += -(otherLocalToWorld.Position - boidPosition) * (1f / math.max(distToOtherBoid, .0001f));
////                            positionSum += otherLocalToWorld.Position;
////                            headingSum += otherLocalToWorld.Forward;

////                            boidsNearby++;
////                        }
////                    }
////                });

////                float3 force = float3.zero;

////                if (boidsNearby > 0) {
////                    force += (seperationSum / boidsNearby)                * controller.separationWeight;
////                    force += ((positionSum / boidsNearby) - boidPosition) * controller.cohesionWeight;
////                    force += (headingSum / boidsNearby)                   * controller.alignmentWeight;
////                }
////                if (math.min(math.min(
////                    (controller.cageSize / 2f) - math.abs(boidPosition.x),
////                    (controller.cageSize / 2f) - math.abs(boidPosition.y)),
////                    (controller.cageSize / 2f) - math.abs(boidPosition.z))
////                        < controller.avoidWallsTurnDist) {
////                    force += -math.normalize(boidPosition) * controller.avoidWallsWeight;
////                }

////                float3 velocity = localToWorld.Forward * controller.boidSpeed;
////                velocity += force * Time.deltaTime;
////                velocity = math.normalize(velocity) * controller.boidSpeed;

////                newBoidPositions[boidIndex] = float4x4.TRS(
////                    localToWorld.Position + velocity * Time.d,
////                    quaternion.LookRotationSafe(velocity, localToWorld.Up),
////                    new float3(1f)
////                );
////                boidIndex++;
////            });
            
////            boidIndex = 0;
////            Entities.WithAll<BoidECS>().ForEach((Entity boid, ref LocalToWorld localToWorld) => {
////                localToWorld.Value = newBoidPositions[boidIndex];
////                boidIndex++;
////            });
////        }
////    }
////}