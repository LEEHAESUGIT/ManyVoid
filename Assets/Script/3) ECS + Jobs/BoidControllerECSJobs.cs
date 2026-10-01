using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Unity.Entities.Graphics;
using static UnityEngine.EventSystems.EventTrigger;


// 보이드를 인스턴스하고 컨트롤
public class BoidControllerECSJobs : MonoBehaviour
{
	// 싱글톤 패턴
	public static BoidControllerECSJobs Instance;

	[SerializeField] private int boidAmount;			// 인스턴스할 개체 갯수
	[SerializeField] private Mesh sharedMesh;			// 인스턴스할 개체 매쉬
	[SerializeField] private Material sharedMaterial;	// 인스턴스할 개체 마테리얼

	public float boidSpeed;				// 개체의 스피드
	public float boidPerceptionRadius;	// 개첵 주변을 인식할수 있는 거리
	public float cageSize;              // 군체의 움직임 범위

	/// <summary>
	/// Boids Flocking
	/// 군집 알고리즘의 중요 요소
	/// Separation : 분리
	///		서로 충돌 하는것을 막아준다.
	/// alignment: 정력력
	///		군집 내부에서 같은 방향으로 날아가게 한다.
	/// cohesion: 응집력
	///		주변 개체와 가까이 있기 위해 모인다.
	/// </summary>
	public float separationWeight;		// 군체 내부 개체끼리의 충돌을 막고, 분리되어 움직이도록 하는 힘의 세기(가중치)
	public float cohesionWeight;		// 군체 내부 개체끼리 모이게 하는 힘의 세기.
	public float alignmentWeight;		// 군집 내부 개체들이 같은 방향으로 가게 하는 힘의 세기.

	public float avoidWallsWeight;		// 벽으로 부터 떨어지기 위한 힘의세기
	public float avoidWallsTurnDist;	// 벽으로 부터 피해 회전반경

	private void Awake()
	{
		Instance = this;
		// ECS를 사용하기 위해 EntityManager를 선언.
		EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

		// 보이드에 필요한 컴포넌트 조합으로 아키타입을 생성.
		EntityArchetype boidArchetype = entityManager.CreateArchetype(
			typeof(BoidECSJobs),		// 보이드 식별?
			typeof(LocalTransform)		// 개체의 트랜스폼(위치,회전 등 물리)
		);

		// JobSystem
		// Unity JobSystem을 사용하기 위해 네이티브 메모리 컨테이너를 생성한다. 
		NativeArray<Entity> boidArray = new NativeArray<Entity>(boidAmount, Allocator.TempJob); // 매개변수는 각 인스턴스할 개체의 갯수, 임시 메모리
		// 생성된 아키타입과, JobSystem을 위한 네이티브 메모리 컨테이너를 요소로 엔티티를 생성한다.
		entityManager.CreateEntity(boidArchetype, boidArray);
		// RenderMeshDescription : 그래픽 엔티티를 설정하고 구성.
		RenderMeshDescription renderMeshDescription = new RenderMeshDescription(shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.Off,	// 그림자를 생성하지 않겠다.
																				receiveShadows: false);                                         // 그림자를 받지 않겠다.
		// RenderMeshArray :  메쉬와 마테리얼을 포함하는 공유 구성 요소
		RenderMeshArray renderMeshArray = new RenderMeshArray(new Material[] { sharedMaterial }, new Mesh[] { sharedMesh }); // 받아온 마테리얼과 매쉬로 배열생성.

		// 엔티티 개수 만큼 반복문을 실행.
		for (int i = 0; i < boidArray.Length; i++)
		{
			// 엔티티(boidArray[i])의 위치,회전,스케일 정보를 새 값으로 바꾼다.
			entityManager.SetComponentData(boidArray[i], LocalTransform.FromPositionRotationScale
				(
					RandomPosition(),	// 랜덤 위치 값
					RandomRotation(),	// 랜덤 회전 값
					1f					// 스케일 -> 크기 1배
				)
			);
			// Entities Graphics 패키지와 호환되도록 엔티티를 채우는 정적 메서드를 포함하는 헬퍼 클래스
			RenderMeshUtility.AddComponents(
								boidArray[i],	// Entity
								entityManager,	// EntityManager
								renderMeshDescription,	// 엔티티가 어떻게 랜더링 될지 결정
								renderMeshArray,		// 사용할 매쉬와 마테리얼 구성요소 배열
								MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0) // renderMeshArray를 인덱싱 하는데 사용
								);
		}
		// 네이티브 메모리 컴포넌트 를 해제한다.
		boidArray.Dispose();
	}
	// 랜덤 위치
	private float3 RandomPosition()
	{
		return new float3(
			UnityEngine.Random.Range(-cageSize / 2f, cageSize / 2f),	// X
			UnityEngine.Random.Range(-cageSize / 2f, cageSize / 2f),	// Y
			UnityEngine.Random.Range(-cageSize / 2f, cageSize / 2f)		// Z
		);
	}
	// 랜덤 회전
	private quaternion RandomRotation()
	{
		return quaternion.Euler(
			UnityEngine.Random.Range(-360f, 360f),	// X
			UnityEngine.Random.Range(-360f, 360f),	// Y
			UnityEngine.Random.Range(-360f, 360f)	// Z
		);
	}

	// 케이지 기즈모(선그리기) 육면체가 나타난다.
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

//public class BoidControllerECSJobs : MonoBehaviour {

//    public static BoidControllerECSJobs Instance;

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
//            typeof(BoidECSJobs),
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

//            entityManager.SetComponentData<RenderMesh>(boidArray[i]);

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
