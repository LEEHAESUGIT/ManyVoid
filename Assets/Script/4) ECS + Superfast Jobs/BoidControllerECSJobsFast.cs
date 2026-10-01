using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI.Table;

public class BoidControllerECSJobsFast : MonoBehaviour
{

	public static BoidControllerECSJobsFast Instance;

	[SerializeField] private int boidAmount;
	[SerializeField] private Mesh sharedMesh;
	[SerializeField] private Material sharedMaterial;

	public float boidSpeed;
	public float boidPerceptionRadius;
	public float cageSize;

	public float separationWeight;
	public float cohesionWeight;
	public float alignmentWeight;

	public float avoidWallsWeight;
	public float avoidWallsTurnDist;

	private void Awake()
	{

		Instance = this;

		EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

		EntityArchetype boidArchetype = entityManager.CreateArchetype(
			typeof(BoidECSJobsFast),
			typeof(LocalTransform),
			typeof(BoidSystemECSJobsFast.Velocity)
		);

		NativeArray<Entity> boidArray = new NativeArray<Entity>(boidAmount, Allocator.TempJob);
		entityManager.CreateEntity(boidArchetype, boidArray);

		// RenderMeshDescription : 그래픽 엔티티를 설정하고 구성.
		RenderMeshDescription renderMeshDescription = new RenderMeshDescription(shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off, // 그림자를 생성하지 않겠다.
																				receiveShadows: false);                                         // 그림자를 받지 않겠다.
																																				// RenderMeshArray :  메쉬와 마테리얼을 포함하는 공유 구성 요소
		RenderMeshArray renderMeshArray = new RenderMeshArray(new Material[] { sharedMaterial }, new Mesh[] { sharedMesh }); // 받아온 마테리얼과 매쉬로 배열생성.


		for (int i = 0; i < boidArray.Length; i++)
		{
			quaternion rot = RandomRotation();
			Unity.Mathematics.Random rand = new Unity.Mathematics.Random((uint)i + 1);
			entityManager.SetComponentData(boidArray[i], LocalTransform.FromPositionRotationScale(
							RandomPosition(),
							rot,
							1f
							));

			entityManager.SetComponentData(boidArray[i], new BoidSystemECSJobsFast.Velocity
			{
				Value = math.forward(rot) * boidSpeed
			});



			// Entities Graphics 패키지와 호환되도록 엔티티를 채우는 정적 메서드를 포함하는 헬퍼 클래스
			RenderMeshUtility.AddComponents(
								boidArray[i],   // Entity
								entityManager,  // EntityManager
								renderMeshDescription,  // 엔티티가 어떻게 랜더링 될지 결정
								renderMeshArray,        // 사용할 매쉬와 마테리얼 구성요소 배열
								MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0) // renderMeshArray를 인덱싱 하는데 사용
								);
		}

		boidArray.Dispose();
	}

	private float3 RandomPosition()
	{
		return new float3(
			UnityEngine.Random.Range(-cageSize / 2f, cageSize / 2f),
			UnityEngine.Random.Range(-cageSize / 2f, cageSize / 2f),
			UnityEngine.Random.Range(-cageSize / 2f, cageSize / 2f)
		);
	}
	private quaternion RandomRotation()
	{
		return quaternion.Euler(
			UnityEngine.Random.Range(-360f, 360f),
			UnityEngine.Random.Range(-360f, 360f),
			UnityEngine.Random.Range(-360f, 360f)
		);
	}

	private void OnDrawGizmos()
	{

		Gizmos.color = Color.red;
		Gizmos.DrawWireCube(
			Vector3.zero,
			new Vector3(
				cageSize,
				cageSize,
				cageSize
			)
		);
	}
}



//using UnityEngine;
//using Unity.Entities;
//using Unity.Rendering;
//using Unity.Transforms;
//using Unity.Collections;
//using Unity.Mathematics;

//public class BoidControllerECSJobsFast : MonoBehaviour {

//    public static BoidControllerECSJobsFast Instance;

//    [SerializeField] private int boidAmount;
//    [SerializeField] private Mesh sharedMesh;
//    [SerializeField] private Material sharedMaterial;

//    public float boidSpeed;
//    public float boidPerceptionRadius;
//    public float cageSize;

//    public float separationWeight;
//    public float cohesionWeight;
//    public float alignmentWeight;

//    public float avoidWallsWeight;
//    public float avoidWallsTurnDist;

//    private void Awake() {

//        Instance = this;

//        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

//        EntityArchetype boidArchetype = entityManager.CreateArchetype(
//            typeof(BoidECSJobsFast),
//            typeof(RenderMesh),
//            typeof(RenderBounds),
//            typeof(LocalToWorld)
//        );

//        NativeArray<Entity> boidArray = new NativeArray<Entity>(boidAmount, Allocator.Temp);
//        entityManager.CreateEntity(boidArchetype, boidArray);

//        for (int i = 0; i < boidArray.Length; i++) {
//            Unity.Mathematics.Random rand = new Unity.Mathematics.Random((uint)i + 1);
//            entityManager.SetComponentData(boidArray[i], new LocalToWorld {
//                Value = float4x4.TRS(
//                    RandomPosition(),
//                    RandomRotation(),
//                    new float3(1f))
//            });
//            entityManager.SetSharedComponentData(boidArray[i], new RenderMesh {
//                mesh = sharedMesh,
//                material = sharedMaterial,
//            });
//        }

//        boidArray.Dispose();
//    }

//    private float3 RandomPosition() {
//        return new float3(
//            UnityEngine.Random.Range(-cageSize / 2f, cageSize / 2f),
//            UnityEngine.Random.Range(-cageSize / 2f, cageSize / 2f),
//            UnityEngine.Random.Range(-cageSize / 2f, cageSize / 2f)
//        );
//    }
//    private quaternion RandomRotation() {
//        return quaternion.Euler(
//            UnityEngine.Random.Range(-360f, 360f),
//            UnityEngine.Random.Range(-360f, 360f),
//            UnityEngine.Random.Range(-360f, 360f)
//        );
//    }

//    private void OnDrawGizmos() {

//        Gizmos.color = Color.red;
//        Gizmos.DrawWireCube(
//            Vector3.zero,
//            new Vector3(
//                cageSize,
//                cageSize,
//                cageSize
//            )
//        );
//    }
//}
