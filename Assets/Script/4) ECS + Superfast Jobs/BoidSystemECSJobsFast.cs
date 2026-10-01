/** 
* Parts of this code were originally made by Bogdan Codreanu
* Original code: https://github.com/BogdanCodreanu/ECS-Boids-Murmuration_Unity_2019.1
*/

//https://www.youtube.com/watch?v=mNZq0RhM-98

using Unity.Jobs;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.UIElements;
using System.Security.Cryptography;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class BoidSystemECSJobsFast : SystemBase
{

	private EntityQuery boidGroup;
	private BoidControllerECSJobsFast controller;

	public struct Velocity : IComponentData
	{
		public float3 Value;
	}
	[BurstCompile]
	partial struct CopyJob : IJobEntity
	{
		public NativeArray<float3> Positions;
		public NativeArray<float3> Velocities;

		public void Execute([EntityIndexInQuery] int index, in LocalTransform transform, in Velocity velocity)
		{
			Positions[index] = transform.Position;

			Velocities[index] = velocity.Value;
		}
	}

	public struct HashAndIndex
	{
		public int Hash;
		public int BoidIndex;
	}
	[BurstCompile]
	public struct BuildHashJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<float3> Positions;
		public NativeArray<HashAndIndex> hashes;
		public float cellRadius;

		public quaternion CellRotation;
		public float3 PositionOffset;


		public void Execute(int index)
		{
			float3 p = math.mul(CellRotation, Positions[index] + PositionOffset);
			int hash = (int)math.hash((int3)math.floor(p / cellRadius));
			hashes[index] = new HashAndIndex { Hash = hash, BoidIndex = index };
		}
	}

	public struct HashCompare : IComparer<HashAndIndex>
	{
		public int Compare(HashAndIndex a, HashAndIndex b)
		{
			return a.Hash.CompareTo(b.Hash);
		}

	}

	[BurstCompile]
	private struct MergeCellsJob : IJob
	{
		[ReadOnly] public NativeArray<HashAndIndex> Sorted;
		[ReadOnly] public NativeArray<float3> Positions;
		[ReadOnly] public NativeArray<float3> Velocities;

		public NativeArray<int> CellIndices;

		public NativeArray<float3> CellPositions;
		public NativeArray<float3> CellHeadings;
		public NativeArray<int> CellCounts;

		public void Execute()
		{
			if (Sorted.Length == 0)
				return;

			int currentHash = Sorted[0].Hash;
			int representative = Sorted[0].BoidIndex;

			float3 posSum = float3.zero;
			float3 velSum = float3.zero;
			int count = 0;

			for (int i = 0; i < Sorted.Length; i++)
			{
				var item = Sorted[i];

				// 새 Cell 시작
				if (item.Hash != currentHash)
				{
					// 이전 Cell 저장
					CellPositions[representative] = posSum;
					CellHeadings[representative] = velSum;
					CellCounts[representative] = count;

					// 새 Cell 시작
					currentHash = item.Hash;
					representative = item.BoidIndex;

					posSum = float3.zero;
					velSum = float3.zero;
					count = 0;
				}
				

				posSum += Positions[item.BoidIndex];
				velSum += Velocities[item.BoidIndex];
				count++;

				CellIndices[item.BoidIndex] = representative;
			}
			// 마지막 Cell 저장
			CellPositions[representative] = posSum;
			CellHeadings[representative] = velSum;
			CellCounts[representative] = count;
		}
	}

	[BurstCompile]
	private partial struct MoveJob : IJobEntity
	{
		[ReadOnly] public float deltaTime;
		[ReadOnly] public float boidSpeed;

		[ReadOnly] public float separationWeight;
		[ReadOnly] public float alignmentWeight;
		[ReadOnly] public float cohesionWeight;

		[ReadOnly] public float cageSize;
		[ReadOnly] public float cageAvoidDist;
		[ReadOnly] public float cageAvoidWeight;

		[ReadOnly] public float cellSize;
		[ReadOnly] public NativeArray<int> CellIndices;
		[ReadOnly] public NativeArray<float3> CellPositions;
		[ReadOnly] public NativeArray<float3> CellHeadings;
		[ReadOnly] public NativeArray<int> CellCounts;


		public void Execute([EntityIndexInQuery] int index, ref LocalTransform localTransform, ref Velocity velocity)
		{
			int rep = CellIndices[index];

			if (CellCounts[rep] <= 0)
				return;

			float3 boidPosition = localTransform.Position;
			int cellIndex = rep;

			int nearbyBoidCount = CellCounts[rep] - 1;
			float3 positionSum = CellPositions[cellIndex] - localTransform.Position;
			float3 headingSum = CellHeadings[cellIndex] - velocity.Value;
			float3 force = float3.zero;



			if (nearbyBoidCount > 0)
			{
				float3 averagePosition = positionSum / nearbyBoidCount;

				float distToAveragePositionSq = math.lengthsq(averagePosition - boidPosition);
				float maxDistToAveragePositionSq = cellSize * cellSize;

				float distanceNormalized = distToAveragePositionSq / maxDistToAveragePositionSq;
				float needToLeave = math.max(1 - distanceNormalized, 0f);

				float3 toAveragePosition = math.normalizesafe(averagePosition - boidPosition);
				float3 averageHeading = headingSum / nearbyBoidCount;

				force += -toAveragePosition * separationWeight * needToLeave;
				force += toAveragePosition * cohesionWeight;
				force += averageHeading * alignmentWeight;
			}

			if (math.min(math.min(
			(cageSize / 2f) - math.abs(boidPosition.x),
			(cageSize / 2f) - math.abs(boidPosition.y)),
			(cageSize / 2f) - math.abs(boidPosition.z))	< cageAvoidDist)
			{
				force += -math.normalize(boidPosition) * cageAvoidWeight;
			}

			velocity.Value += force * deltaTime;


			if (math.lengthsq(velocity.Value) < 0.0001f)
				velocity.Value = localTransform.Forward();

			if (math.lengthsq(velocity.Value) > 0.0001f)
				velocity.Value = math.normalizesafe(velocity.Value) * boidSpeed;
			
			localTransform.Position += velocity.Value * deltaTime;
			localTransform.Rotation = quaternion.LookRotationSafe(velocity.Value, math.up());
		}
	}

	protected override void OnCreate()
	{
		boidGroup = GetEntityQuery(new EntityQueryDesc
		{
			All = new[] {
				ComponentType.ReadOnly<BoidECSJobsFast>(),
				ComponentType.ReadWrite<LocalTransform>(),
				ComponentType.ReadWrite<Velocity>()
			}
		});
	}

	protected override void OnUpdate()
	{
		if (!controller)
		{
			controller = BoidControllerECSJobsFast.Instance;
		}
		if (controller)
		{
			int boidCount = boidGroup.CalculateEntityCount();
		Debug.Log(boidCount);

			var positions = new NativeArray<float3>(boidCount, Allocator.TempJob);
			var velocities = new NativeArray<float3>(boidCount, Allocator.TempJob);
			var hashes = new NativeArray<HashAndIndex>(boidCount, Allocator.TempJob);
			var cellIndices = new NativeArray<int>(boidCount, Allocator.TempJob);
			var cellBoidCount = new NativeArray<int>(boidCount, Allocator.TempJob);
			var cellPositions = new NativeArray<float3>(boidCount, Allocator.TempJob);
			var cellHeadings = new NativeArray<float3>(boidCount, Allocator.TempJob);


			var copyJob = new CopyJob
			{
				Positions = positions,
				Velocities = velocities,
			};
			var copyHandle = copyJob.ScheduleParallel(Dependency);



			//quaternion randomHashRotation = quaternion.Euler(
			//	UnityEngine.Random.Range(-360f, 360f),
			//	UnityEngine.Random.Range(-360f, 360f),
			//	UnityEngine.Random.Range(-360f, 360f)
			//);
			quaternion randomHashRotation = quaternion.Euler(
				UnityEngine.Random.Range(-15f, 15f),
				UnityEngine.Random.Range(-15f, 15f),
				UnityEngine.Random.Range(-15f, 15f)
			);
			float offsetRange = controller.boidPerceptionRadius / 2f;
			float3 randomHashOffset = new float3(
				UnityEngine.Random.Range(-offsetRange, offsetRange),
				UnityEngine.Random.Range(-offsetRange, offsetRange),
				UnityEngine.Random.Range(-offsetRange, offsetRange)
			);

			var hashJob = new BuildHashJob
			{
				Positions = positions,
				hashes = hashes,
				cellRadius = controller.boidPerceptionRadius,

				CellRotation = randomHashRotation,
				PositionOffset = randomHashOffset
			};
			var hashHandle = hashJob.Schedule(boidCount, 64, copyHandle);
			hashHandle.Complete();
			hashes.Sort(new HashCompare());

			var mergeCellsJob = new MergeCellsJob
			{
				Sorted = hashes,

				Positions = positions,
				Velocities = velocities,

				CellIndices = cellIndices,

				CellPositions = cellPositions,
				CellHeadings = cellHeadings,
				CellCounts = cellBoidCount
			};
			var mergeHandle = mergeCellsJob.Schedule(hashHandle);

			var moveJob = new MoveJob
			{
				deltaTime = SystemAPI.Time.DeltaTime,
				boidSpeed = controller.boidSpeed,

				separationWeight = controller.separationWeight,
				alignmentWeight = controller.alignmentWeight,
				cohesionWeight = controller.cohesionWeight,

				cageSize = controller.cageSize,
				cageAvoidDist = controller.avoidWallsTurnDist,
				cageAvoidWeight = controller.avoidWallsWeight,

				cellSize = controller.boidPerceptionRadius,

				CellIndices = cellIndices,
				CellPositions = cellPositions,
				CellHeadings = cellHeadings,
				CellCounts = cellBoidCount
			};
			Dependency = moveJob.ScheduleParallel(mergeHandle);

			positions.Dispose(Dependency);
			velocities.Dispose(Dependency);
			hashes.Dispose(Dependency);

			cellIndices.Dispose(Dependency);
			cellBoidCount.Dispose(Dependency);
			cellPositions.Dispose(Dependency);
			cellHeadings.Dispose(Dependency);
		}
	}
}







//[BurstCompile]
//private partial struct MoveBoids : IJobEntity
//{

//	[ReadOnly] public float deltaTime;
//	[ReadOnly] public float boidSpeed;

//	[ReadOnly] public float separationWeight;
//	[ReadOnly] public float alignmentWeight;
//	[ReadOnly] public float cohesionWeight;

//	[ReadOnly] public float cageSize;
//	[ReadOnly] public float cageAvoidDist;
//	[ReadOnly] public float cageAvoidWeight;

//	[ReadOnly] public float cellSize;
//	[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellIndices;
//	[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> positionSumsOfCells;
//	[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> headingSumsOfCells;
//	[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellBoidCount;

//	public void Execute(Entity boid, int boidIndex, ref LocalTransform localTransform)
//	{

//		float3 boidPosition = localTransform.Position;
//		int cellIndex = cellIndices[boidIndex];

//		int nearbyBoidCount = cellBoidCount[cellIndex] - 1;
//		float3 positionSum = positionSumsOfCells[cellIndex] - localTransform.Position;
//		float3 headingSum = headingSumsOfCells[cellIndex] - localTransform.Forward();

//		float3 force = float3.zero;

//		if (nearbyBoidCount > 0)
//		{
//			float3 averagePosition = positionSum / nearbyBoidCount;

//			float distToAveragePositionSq = math.lengthsq(averagePosition - boidPosition);
//			float maxDistToAveragePositionSq = cellSize * cellSize;

//			float distanceNormalized = distToAveragePositionSq / maxDistToAveragePositionSq;
//			float needToLeave = math.max(1 - distanceNormalized, 0f);

//			float3 toAveragePosition = math.normalizesafe(averagePosition - boidPosition);
//			float3 averageHeading = headingSum / nearbyBoidCount;

//			force += -toAveragePosition * separationWeight * needToLeave;
//			force += toAveragePosition * cohesionWeight;
//			force += averageHeading * alignmentWeight;
//		}

//		if (math.min(math.min(
//			(cageSize / 2f) - math.abs(boidPosition.x),
//			(cageSize / 2f) - math.abs(boidPosition.y)),
//			(cageSize / 2f) - math.abs(boidPosition.z))
//				< cageAvoidDist)
//		{
//			force += -math.normalize(boidPosition) * cageAvoidWeight;
//		}

//		float3 velocity = localTransform.Forward() * boidSpeed;
//		velocity += force * deltaTime;
//		velocity = math.normalize(velocity) * boidSpeed;

//		localTransform.Value = float4x4.TRS(
//			localTransform.Position + velocity * deltaTime,
//			quaternion.LookRotationSafe(velocity, localTransform.Up),
//			new float3(1f)
//		);
//	}
//}




























///** 
//* Parts of this code were originally made by Bogdan Codreanu
//* Original code: https://github.com/BogdanCodreanu/ECS-Boids-Murmuration_Unity_2019.1
//*/ 

//using Unity.Jobs;
//using Unity.Burst;
//using Unity.Entities;
//using Unity.Transforms;
//using Unity.Mathematics;
//using Unity.Collections;

//public class BoidSystemECSJobsFast : JobComponentSystem
//{

//	private EntityQuery boidGroup;
//	private BoidControllerECSJobsFast controller;

//	 Copies all boid positions and headings into buffer
//	[BurstCompile]
//	[RequireComponentTag(typeof(BoidECSJobsFast))]
//	private struct CopyPositionsAndHeadingsInBuffer : IJobForEachWithEntity<LocalToWorld>
//	{
//		public NativeArray<float3> boidPositions;
//		public NativeArray<float3> boidHeadings;

//		public void Execute(Entity boid, int boidIndex, [ReadOnly] ref LocalToWorld localToWorld)
//		{
//			boidPositions[boidIndex] = localToWorld.Position;
//			boidHeadings[boidIndex] = localToWorld.Forward;
//		}
//	}

//	 Asigns each boid to a cell. Each boid index is stored in the hashMap. Each hash corresponds to a cell.
//	 The cell grid has a random offset and rotation each frame to remove artefacts.
//	[BurstCompile]
//	[RequireComponentTag(typeof(BoidECSJobsFast))]
//	private struct HashPositionsToHashMap : IJobForEachWithEntity<LocalToWorld>
//	{
//		public NativeMultiHashMap<int, int>.ParallelWriter hashMap;
//		[ReadOnly] public quaternion cellRotationVary;
//		[ReadOnly] public float3 positionOffsetVary;
//		[ReadOnly] public float cellRadius;

//		public void Execute(Entity boid, int boidIndex, [ReadOnly] ref LocalToWorld localToWorld)
//		{
//			var hash = (int)math.hash(new int3(math.floor(math.mul(cellRotationVary, localToWorld.Position + positionOffsetVary) / cellRadius)));
//			hashMap.Add(hash, boidIndex);
//		}
//	}

//	 Sums up positions and headings of all boids of each cell. These sums are stored in the
//	 same array as before (cellPositions and cellHeadings), so that there is no need for
//	 a new array. The index of each cell is set to the index of the first boid.
//	 With the array indicesOfCells each boid can find the index of its cell.
//	 This way every boid knows the sum of all the positions (and headings) of all the other
//	 boids in the same cell -> no nested loop required -> massive performance boost
//	[BurstCompile]
//	private struct MergeCellsJob : IJobNativeMultiHashMapMergedSharedKeyIndices
//	{
//		public NativeArray<int> indicesOfCells;
//		public NativeArray<float3> cellPositions;
//		public NativeArray<float3> cellHeadings;
//		public NativeArray<int> cellCount;

//		public void ExecuteFirst(int firstBoidIndexEncountered)
//		{
//			indicesOfCells[firstBoidIndexEncountered] = firstBoidIndexEncountered;
//			cellCount[firstBoidIndexEncountered] = 1;
//			float3 positionInThisCell = cellPositions[firstBoidIndexEncountered] / cellCount[firstBoidIndexEncountered];
//		}

//		public void ExecuteNext(int firstBoidIndexAsCellKey, int boidIndexEncountered)
//		{
//			cellCount[firstBoidIndexAsCellKey] += 1;
//			cellHeadings[firstBoidIndexAsCellKey] += cellHeadings[boidIndexEncountered];
//			cellPositions[firstBoidIndexAsCellKey] += cellPositions[boidIndexEncountered];
//			indicesOfCells[boidIndexEncountered] = firstBoidIndexAsCellKey;
//		}
//	}

//	 Calculates the forces for each boid (no nested loop). All forces are weighted, added up
//	 and directly applied to orientation and position.
//	[BurstCompile]
//	[RequireComponentTag(typeof(BoidECSJobsFast))]
//	private struct MoveBoids : IJobForEachWithEntity<LocalToWorld>
//	{

//		[ReadOnly] public float deltaTime;
//		[ReadOnly] public float boidSpeed;

//		[ReadOnly] public float separationWeight;
//		[ReadOnly] public float alignmentWeight;
//		[ReadOnly] public float cohesionWeight;

//		[ReadOnly] public float cageSize;
//		[ReadOnly] public float cageAvoidDist;
//		[ReadOnly] public float cageAvoidWeight;

//		[ReadOnly] public float cellSize;
//		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellIndices;
//		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> positionSumsOfCells;
//		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> headingSumsOfCells;
//		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellBoidCount;

//		public void Execute(Entity boid, int boidIndex, ref LocalToWorld localToWorld)
//		{

//			float3 boidPosition = localToWorld.Position;
//			int cellIndex = cellIndices[boidIndex];

//			int nearbyBoidCount = cellBoidCount[cellIndex] - 1;
//			float3 positionSum = positionSumsOfCells[cellIndex] - localToWorld.Position;
//			float3 headingSum = headingSumsOfCells[cellIndex] - localToWorld.Forward;

//			float3 force = float3.zero;

//			if (nearbyBoidCount > 0)
//			{
//				float3 averagePosition = positionSum / nearbyBoidCount;

//				float distToAveragePositionSq = math.lengthsq(averagePosition - boidPosition);
//				float maxDistToAveragePositionSq = cellSize * cellSize;

//				float distanceNormalized = distToAveragePositionSq / maxDistToAveragePositionSq;
//				float needToLeave = math.max(1 - distanceNormalized, 0f);

//				float3 toAveragePosition = math.normalizesafe(averagePosition - boidPosition);
//				float3 averageHeading = headingSum / nearbyBoidCount;

//				force += -toAveragePosition * separationWeight * needToLeave;
//				force += toAveragePosition * cohesionWeight;
//				force += averageHeading * alignmentWeight;
//			}

//			if (math.min(math.min(
//				(cageSize / 2f) - math.abs(boidPosition.x),
//				(cageSize / 2f) - math.abs(boidPosition.y)),
//				(cageSize / 2f) - math.abs(boidPosition.z))
//					< cageAvoidDist)
//			{
//				force += -math.normalize(boidPosition) * cageAvoidWeight;
//			}

//			float3 velocity = localToWorld.Forward * boidSpeed;
//			velocity += force * deltaTime;
//			velocity = math.normalize(velocity) * boidSpeed;

//			localToWorld.Value = float4x4.TRS(
//				localToWorld.Position + velocity * deltaTime,
//				quaternion.LookRotationSafe(velocity, localToWorld.Up),
//				new float3(1f)
//			);
//		}
//	}

//	protected override void OnCreate()
//	{
//		boidGroup = GetEntityQuery(new EntityQueryDesc
//		{
//			All = new[] { ComponentType.ReadOnly<BoidECSJobsFast>(), ComponentType.ReadWrite<LocalToWorld>() },
//			Options = EntityQueryOptions.FilterWriteGroup
//		});
//	}

//	protected override JobHandle OnUpdate(JobHandle inputDeps)
//	{

//		if (!controller)
//		{
//			controller = BoidControllerECSJobsFast.Instance;
//		}
//		if (controller)
//		{
//			int boidCount = boidGroup.CalculateEntityCount();

//			var cellIndices = new NativeArray<int>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
//			var cellBoidCount = new NativeArray<int>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
//			var boidPositions = new NativeArray<float3>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
//			var boidHeadings = new NativeArray<float3>(boidCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
//			var hashMap = new NativeMultiHashMap<int, int>(boidCount, Allocator.TempJob);

//			var positionsAndHeadingsCopyJob = new CopyPositionsAndHeadingsInBuffer
//			{
//				boidPositions = boidPositions,
//				boidHeadings = boidHeadings
//			};
//			JobHandle positionsAndHeadingsCopyJobHandle = positionsAndHeadingsCopyJob.Schedule(boidGroup, inputDeps);

//			quaternion randomHashRotation = quaternion.Euler(
//				UnityEngine.Random.Range(-360f, 360f),
//				UnityEngine.Random.Range(-360f, 360f),
//				UnityEngine.Random.Range(-360f, 360f)
//			);
//			float offsetRange = controller.boidPerceptionRadius / 2f;
//			float3 randomHashOffset = new float3(
//				UnityEngine.Random.Range(-offsetRange, offsetRange),
//				UnityEngine.Random.Range(-offsetRange, offsetRange),
//				UnityEngine.Random.Range(-offsetRange, offsetRange)
//			);

//			var hashPositionsJob = new HashPositionsToHashMap
//			{
//				hashMap = hashMap.AsParallelWriter(),
//				cellRotationVary = randomHashRotation,
//				positionOffsetVary = randomHashOffset,
//				cellRadius = controller.boidPerceptionRadius,
//			};
//			JobHandle hashPositionsJobHandle = hashPositionsJob.Schedule(boidGroup, inputDeps);

//			 Proceed when these two jobs have been completed
//			JobHandle copyAndHashJobHandle = JobHandle.CombineDependencies(
//				positionsAndHeadingsCopyJobHandle,
//				hashPositionsJobHandle
//			);

//			var mergeCellsJob = new MergeCellsJob
//			{
//				indicesOfCells = cellIndices,
//				cellPositions = boidPositions,
//				cellHeadings = boidHeadings,
//				cellCount = cellBoidCount,
//			};
//			JobHandle mergeCellsJobHandle = mergeCellsJob.Schedule(hashMap, 64, copyAndHashJobHandle);

//			var moveJob = new MoveBoids
//			{
//				deltaTime = Time.DeltaTime,
//				boidSpeed = controller.boidSpeed,

//				separationWeight = controller.separationWeight,
//				alignmentWeight = controller.alignmentWeight,
//				cohesionWeight = controller.cohesionWeight,

//				cageSize = controller.cageSize,
//				cageAvoidDist = controller.avoidWallsTurnDist,
//				cageAvoidWeight = controller.avoidWallsWeight,

//				cellSize = controller.boidPerceptionRadius,
//				cellIndices = cellIndices,
//				positionSumsOfCells = boidPositions,
//				headingSumsOfCells = boidHeadings,
//				cellBoidCount = cellBoidCount,
//			};
//			JobHandle moveJobHandle = moveJob.Schedule(boidGroup, mergeCellsJobHandle);
//			moveJobHandle.Complete();
//			hashMap.Dispose();

//			inputDeps = moveJobHandle;
//			boidGroup.AddDependency(inputDeps);

//			return inputDeps;
//		}
//		else
//		{
//			return inputDeps;
//		}
//	}
//}
