using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Rendering;
using Unity.Entities.Graphics;
public partial class BoidSystemECSJobs : SystemBase
{
	// 컨트롤러 호출
	private BoidControllerECSJobs controller;

	protected override void OnUpdate() // SystemBase의 구성요소
	{
		// This runs only if there exists a BoidControllerECSJobs instance.	
		if (!controller)
		{
			controller = BoidControllerECSJobs.Instance;
		}
		if (controller)
		{
			// ECS에 저장된 엔티티를 구성 컴포넌트를 조건으로 사용하는 쿼리문으로 찾음.
			EntityQuery boidQuery = GetEntityQuery(ComponentType.ReadOnly<BoidECSJobs>(), ComponentType.ReadOnly<LocalTransform>());
			// 쿼리 결과에 맞는 엔티티들을 엔티티 배열로 정렬해 반환한다.
			NativeArray<Entity> entityArray = boidQuery.ToEntityArray(Allocator.TempJob);
			// 쿼리 결과에 맞는 데이터 객체를 배열로 정렬해 반환한다. 
			NativeArray<LocalTransform> transformArray = boidQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

			// These arrays get deallocated after job completion
			// 네이티브 메모리 컨테이너를 쿼리결과로 찾은 엔티티의 갯수만큼 할당한다. (할당 자료형은 엔티티+트랜스폼 정보로 이루어진 구조체다.)
			NativeArray<EntityWithLocalTransform> boidArray = new NativeArray<EntityWithLocalTransform>(entityArray.Length, Allocator.TempJob);

			for (int i = 0; i < entityArray.Length; i++)
			{
				// 할당한 네이티브 메모리에 쿼리로 찾은 정보들을 인덱스에 맞게 집어넣는다. 
				boidArray[i] = new EntityWithLocalTransform
				{
					entity = entityArray[i],
					localTransform = transformArray[i]
				};
			}
			entityArray.Dispose();      // 엔티티. 네이티브 메모리 컨테이너 할당해제
			transformArray.Dispose();   // 트랜스폼. 네이티브 메모리 컨테이너 할당해제

			// job 에서 사용할 개체 정보 저장
			BoidJob boidJob = new BoidJob
			{
				otherBoids = boidArray,
				boidPerceptionRadius = controller.boidPerceptionRadius,
				separationWeight = controller.separationWeight,
				cohesionWeight = controller.cohesionWeight,
				alignmentWeight = controller.alignmentWeight,
				cageSize = controller.cageSize,
				avoidWallsTurnDist = controller.avoidWallsTurnDist,
				avoidWallsWeight = controller.avoidWallsWeight,
				boidSpeed = controller.boidSpeed,
				deltaTime = SystemAPI.Time.DeltaTime
			};

			// Dependency : JobHandle , 이 job이 기다려야 하는 이전 작업들
			// 작업을 실행하고 -> 새로운 예약을 대기
			Dependency = boidJob.ScheduleParallel(Dependency);  // 이 job 을 여러 스레드에서 병렬 처리 한다.

		}
	}
	// 엔티티와 트랜스폼 정보가 저장되어 있는 구조체
	private struct EntityWithLocalTransform
	{
		public Entity entity;
		public LocalTransform localTransform;
	}

	[BurstCompile] // 유니티의 버스트 컴파일러를 사용한다.
	private partial struct BoidJob : IJobEntity // IJobEntity : ECS , JobSystem을 쉽게 연결하기 위한 고수준 헬퍼 인터페이스
	{
		//[DeallocateOnJobCompletion] : Job이 끝나면 자동으로 Dispose를 한다.
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<EntityWithLocalTransform> otherBoids;
		[ReadOnly] public float boidPerceptionRadius;
		[ReadOnly] public float separationWeight;
		[ReadOnly] public float cohesionWeight;
		[ReadOnly] public float alignmentWeight;
		[ReadOnly] public float cageSize;
		[ReadOnly] public float avoidWallsTurnDist;
		[ReadOnly] public float avoidWallsWeight;
		[ReadOnly] public float boidSpeed;
		[ReadOnly] public float deltaTime;

		// Execute : Job에서 실제로 작업이 실행되는 함수
		//		직접 호출하지 않음. JobSystem(ScheduleParallel())에서 호출을 한다.
		//		스케듈이 발생할때 Job에 저장해놨던 엔티티배열만큼 순회를 한다. 선형구조처럼 생각 할수 있지만 , chunk기반 병렬 순회다.
		public void Execute(Entity boid, ref LocalTransform localTransform)
		{
			// float3 : float 3개의 묶음, 내부요소 float x,y,z
			float3 boidPosition = localTransform.Position;

			float3 seperationSum = float3.zero;
			float3 positionSum = float3.zero;
			float3 headingSum = float3.zero;

			int boidsNearby = 0;

			for (int otherBoidIndex = 0; otherBoidIndex < otherBoids.Length; otherBoidIndex++)
			{
				// 자신을 제외한 다른 엔티티를 대상으로
				if (boid != otherBoids[otherBoidIndex].entity)
				{

					float3 otherPosition = otherBoids[otherBoidIndex].localTransform.Position;
					// 다른 개체와의 거리를 계산
					float distToOtherBoid = math.length(boidPosition - otherPosition);

					// 다른개체가 인식 거리 내부에 있을때
					if (distToOtherBoid < boidPerceptionRadius)
					{
						// 분리
						// 가까운 개체로부터 멀어지려는 힘 계산
						// -(otherPosition - boidPosition) : 방향계산(방향 반전)
						// (1f / math.max(distToOtherBoid, .0001f)) : 거리 역수 곱 (가까울수록 더 강하게 밀어냄)
						// math.max(distToOtherBoid, .0001f) : 0 나누기 방지
						seperationSum += -(otherPosition - boidPosition) * (1f / math.max(distToOtherBoid, .0001f));
						// 응집
						// 주변 개체들의 위치 합산.
						// 나중에 positionSum / boidsNearby 를 하면 주변 무리 중심점
						// 그리고 그 중심점으로 이동
						positionSum += otherPosition;
						// 정렬
						// 주변 개체들의 방향벡터를 합산
						// 즉 주변 개체들의 평균 방향을 계산할때 사용
						headingSum += math.forward(otherBoids[otherBoidIndex].localTransform.Rotation);
						// 주변 개체들의 개수 (주변 인식 범위 내부에 있는 개체들)
						boidsNearby++;
					}
				}
			}

			float3 force = float3.zero;
			// 주변 개체가 존재할때
			if (boidsNearby > 0)
			{
				// 미리 계산한 분리되려는 힘에 디폴트 힘을 곱하여 최종 분리 되는 힘을 산출
				force += (seperationSum / boidsNearby) * separationWeight;
				// 중심점을 계산하고 자신을 빼서 개체가 중심점을 향하게 한다. + 디폴트 힘을 곱해서 힘을 산출한다.
				force += ((positionSum / boidsNearby) - boidPosition) * cohesionWeight;
				// 주변 개체들의 방향을 합산한것에 개체들의 수를 나눠 평균 방향을 산출한다. 디폴트 힘을 곱해서 힘을 산출한다.
				force += (headingSum / boidsNearby) * alignmentWeight;
			}
			// 군집이 정해진 공간안에 존재하기 위해서 케이지의 벽에 가까워지면 내부로 가는 힘을 추가해준다.
			if (math.min(math.min(
				(cageSize / 2f) - math.abs(boidPosition.x),
				(cageSize / 2f) - math.abs(boidPosition.y)),
				(cageSize / 2f) - math.abs(boidPosition.z))
					< avoidWallsTurnDist)
			{
				force += -math.normalize(boidPosition) * avoidWallsWeight;
			}
			// 현재 방향으로 진행할때 얼마나 빠르게 움직이는지 벡터
			float3 velocity = math.forward(localTransform.Rotation) * boidSpeed;
			// 어느 방향으로 틀어야 하는가.
			velocity += force * deltaTime;
			// 방향만 남기고 1로 정규화를 시키고 속도를 곱해서 이동시킨다. 
			// 즉, 모두 일정한 속도로 움직이게 된다.
			velocity = math.normalize(velocity) * boidSpeed;

			// localTransform은 값 타입이기 때문에 데이터를 복사해서 수정하고 다시 대입하기 위해 새로 선언한다.
			LocalTransform newTransform = localTransform;
			// 프레임 속도와 상관없이 velocity방향으로 일정하게 이동한다.
			newTransform.Position += velocity * deltaTime;
			// velocity방향을 바라보게 회전값을 수정한다.
			newTransform.Rotation =
				quaternion.LookRotationSafe(
					velocity,
					math.up()
				);
			// 계산된 데이터를 개체에 적용한다.
			localTransform = newTransform;
			
		}
	}
}

//using Unity.Jobs;
//using Unity.Burst;
//using Unity.Entities;
//using Unity.Transforms;
//using Unity.Mathematics;
//using Unity.Collections;

//public class BoidSystemECSJobs : JobComponentSystem {

//    private BoidControllerECSJobs controller;

//    protected override JobHandle OnUpdate(JobHandle inputDeps) {

//        // This runs only if there exists a BoidControllerECSJobs instance.
//        if (!controller) {
//            controller = BoidControllerECSJobs.Instance;
//        }
//        if (controller) {
//            EntityQuery boidQuery = GetEntityQuery(ComponentType.ReadOnly<BoidECSJobs>(), ComponentType.ReadOnly<LocalToWorld>());

//            NativeArray<Entity> entityArray = boidQuery.ToEntityArray(Allocator.TempJob);
//            NativeArray<LocalToWorld> localToWorldArray = boidQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);

//            // These arrays get deallocated after job completion
//            NativeArray<EntityWithLocalToWorld> boidArray = new NativeArray<EntityWithLocalToWorld>(entityArray.Length, Allocator.TempJob);
//            NativeArray<float4x4> newBoidTransforms = new NativeArray<float4x4>(entityArray.Length, Allocator.TempJob);

//            for (int i = 0; i < entityArray.Length; i++) {
//                boidArray[i] = new EntityWithLocalToWorld {
//                    entity = entityArray[i],
//                    localToWorld = localToWorldArray[i]
//                };
//            }

//            entityArray.Dispose();
//            localToWorldArray.Dispose();

//            BoidJob boidJob = new BoidJob {
//                otherBoids = boidArray,
//                newBoidTransforms = newBoidTransforms,
//                boidPerceptionRadius = controller.boidPerceptionRadius,
//                separationWeight = controller.separationWeight,
//                cohesionWeight = controller.cohesionWeight,
//                alignmentWeight = controller.alignmentWeight,
//                cageSize = controller.cageSize,
//                avoidWallsTurnDist = controller.avoidWallsTurnDist,
//                avoidWallsWeight = controller.avoidWallsWeight,
//                boidSpeed = controller.boidSpeed,
//                deltaTime = Time.DeltaTime
//            };
//            BoidMoveJob boidMoveJob = new BoidMoveJob {
//                newBoidTransforms = newBoidTransforms
//            };

//            JobHandle boidJobHandle = boidJob.Schedule(this, inputDeps);
//            return boidMoveJob.Schedule(this, boidJobHandle);
//        }
//        else {
//            return inputDeps;
//        }
//    }

//    private struct EntityWithLocalToWorld {
//        public Entity entity;
//        public LocalToWorld localToWorld;
//    }

//    [BurstCompile]
//    [RequireComponentTag(typeof(BoidECSJobs))]
//    private struct BoidJob : IJobForEachWithEntity<LocalToWorld> {

//        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<EntityWithLocalToWorld> otherBoids;
//        [WriteOnly] public NativeArray<float4x4> newBoidTransforms;

//        [ReadOnly] public float boidPerceptionRadius;
//        [ReadOnly] public float separationWeight;
//        [ReadOnly] public float cohesionWeight;
//        [ReadOnly] public float alignmentWeight;
//        [ReadOnly] public float cageSize;
//        [ReadOnly] public float avoidWallsTurnDist;
//        [ReadOnly] public float avoidWallsWeight;
//        [ReadOnly] public float boidSpeed;
//        [ReadOnly] public float deltaTime;

//        public void Execute(Entity boid, int boidIndex, [ReadOnly] ref LocalToWorld localToWorld) {
//            float3 boidPosition = localToWorld.Position;

//            float3 seperationSum = float3.zero;
//            float3 positionSum = float3.zero;
//            float3 headingSum = float3.zero;

//            int boidsNearby = 0;

//            for (int otherBoidIndex = 0; otherBoidIndex < otherBoids.Length; otherBoidIndex++) {
//                if (boid != otherBoids[otherBoidIndex].entity) {

//                    float3 otherPosition = otherBoids[otherBoidIndex].localToWorld.Position;
//                    float distToOtherBoid = math.length(boidPosition - otherPosition);

//                    if (distToOtherBoid < boidPerceptionRadius) {

//                        seperationSum += -(otherPosition - boidPosition) * (1f / math.max(distToOtherBoid, .0001f));
//                        positionSum += otherPosition;
//                        headingSum += otherBoids[otherBoidIndex].localToWorld.Forward;

//                        boidsNearby++;
//                    }
//                }
//            }

//            float3 force = float3.zero;

//            if (boidsNearby > 0) {
//                force += (seperationSum / boidsNearby)                * separationWeight;
//                force += ((positionSum / boidsNearby) - boidPosition) * cohesionWeight;
//                force += (headingSum / boidsNearby)                   * alignmentWeight;
//            }
//            if (math.min(math.min(
//                (cageSize / 2f) - math.abs(boidPosition.x),
//                (cageSize / 2f) - math.abs(boidPosition.y)),
//                (cageSize / 2f) - math.abs(boidPosition.z))
//                    < avoidWallsTurnDist) {
//                force += -math.normalize(boidPosition) * avoidWallsWeight;
//            }

//            float3 velocity = localToWorld.Forward * boidSpeed;
//            velocity += force * deltaTime;
//            velocity = math.normalize(velocity) * boidSpeed;

//            newBoidTransforms[boidIndex] = float4x4.TRS(
//                localToWorld.Position + velocity * deltaTime,
//                quaternion.LookRotationSafe(velocity, localToWorld.Up),
//                new float3(1f)
//            );
//        }
//    }

//    [BurstCompile]
//    [RequireComponentTag(typeof(BoidECSJobs))]
//    private struct BoidMoveJob : IJobForEachWithEntity<LocalToWorld> {

//        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float4x4> newBoidTransforms;

//        public void Execute(Entity boid, int boidIndex, [WriteOnly] ref LocalToWorld localToWorld) {
//            localToWorld.Value = newBoidTransforms[boidIndex];
//        }
//    }
//}
