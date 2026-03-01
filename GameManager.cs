using System;
using Leopotam.EcsLite;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;


public class GameObjectCleanUpSystem : IEcsDestroySystem
{
    public void Destroy(IEcsSystems systems)
    {
        var GameObjects = systems.GetWorld().Filter<GameObjectRef>().Inc<DestroyOnExitTag>().End();
        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();

        foreach (var entity in GameObjects)
        {
            Object.Destroy(gameObjectPool.Get(entity).GO);
            systems.GetWorld().DelEntity(entity);
        }
    }
}


public class PlayerWinSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var shared = systems.GetShared<SharedData>();
        if (shared.points >= shared.target)
        {
            shared.isWon = true;
            foreach (var player in systems.GetWorld().Filter<PlayerTag>().Inc<GameObjectRef>().End())
            {
                var playerPos = systems.GetWorld().GetPool<GameObjectRef>().Get(player).GO.transform.position;
                shared.savedPlayerXPosition = playerPos.x;
            }
        }
    }
}


public class DisplaySystem : IEcsRunSystem,IEcsInitSystem
{
    public void Run(IEcsSystems systems)
    {
        var pointsUpdateEventFilter = systems.GetWorld().Filter<PointsUpdateEvent>().End();

        var pointsUpdateEventPool = systems.GetWorld().GetPool<PointsUpdateEvent>();

        foreach (var pointsUpdate in pointsUpdateEventFilter)
        {
            var newPoints = systems.GetShared<SharedData>().points;
            var target = systems.GetShared<SharedData>().target;
            pointsText.text = $"Points : {newPoints}/{target}";
        }
    }

    public void Init(IEcsSystems systems)
    {
        var target = systems.GetShared<SharedData>().target;

        pointsText.text = $"Points : 0/{target}";
    }
}


public class PointsEarnSystem : IEcsRunSystem, IEcsInitSystem
{
    public void Run(IEcsSystems systems)
    {
        int newPoints = systems.GetWorld().Filter<ObstacleDestroyEvent>().End().GetEntitiesCount();

        var pointsUpdateEventFilter = systems.GetWorld().Filter<PointsUpdateEvent>().End();
        var playerDeadEventFilter = systems.GetWorld().Filter<PlayerDeadEvent>().End();

        var pointsUpdateEventPool = systems.GetWorld().GetPool<PointsUpdateEvent>();
        foreach (var pointsUpdate in pointsUpdateEventFilter)
        {
            systems.GetWorld().DelEntity(pointsUpdate);
        }

        bool updated = false;
        if (newPoints > 0)
        {
            systems.GetShared<SharedData>().points += newPoints;
            updated = true;
        }

        if (playerDeadEventFilter.GetEntitiesCount() > 0)
        {
            systems.GetShared<SharedData>().points = 0;

            updated = true;
        }

        if (updated)
        {
            pointsUpdateEventPool.Add(systems.GetWorld().NewEntity());
        }
    }

    public void Init(IEcsSystems systems)
    {
    }
}


public class PlayerDeadByObstacleSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var playerFiler = systems.GetWorld().Filter<PlayerTag>().Inc<GameObjectRef>().End();
        var obstacleFilter = systems.GetWorld().Filter<ObstacleTag>().Inc<GameObjectRef>().End();
        var lifeTimeFilter = systems.GetWorld().Filter<GameObjectLifeTime>().End();

        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var pointsUpdateEventPool = systems.GetWorld().GetPool<PointsUpdateEvent>();
        var lifeTimePool = systems.GetWorld().GetPool<GameObjectLifeTime>();

        var playerDeadEventFIlter = systems.GetWorld().Filter<PlayerDeadEvent>().End();
        var playerDeadEventPool = systems.GetWorld().GetPool<PlayerDeadEvent>();
        foreach (var playerDead in playerDeadEventFIlter)
        {
            systems.GetWorld().DelEntity(playerDead);
        }

        foreach (var player in playerFiler)
        {
            var playerPosition = gameObjectPool.Get(player).GO.transform.position;
            foreach (var obstacle in obstacleFilter)
            {
                var obstaclePosition = gameObjectPool.Get(obstacle).GO.transform.position;
                if (Vector3.Distance(playerPosition, obstaclePosition) < 1)
                {
                    //dead

                    //remove all lifre time objects
                    foreach (var lifeTime in lifeTimeFilter)
                    {
                        lifeTimePool.Get(lifeTime).deadTime = 0;
                    }

                    playerDeadEventPool.Add(systems.GetWorld().NewEntity());
                }
            }
        }
    }
}

public class BulletDestoryObstacleSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var bulletFilter = systems.GetWorld().Filter<BulletTag>().Inc<GameObjectRef>().End();
        var obstacleFilter = systems.GetWorld().Filter<ObstacleTag>().Inc<GameObjectRef>().End();
        var obstalceDesotoryEventFilter = systems.GetWorld().Filter<ObstacleDestroyEvent>().End();

        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var obstalceDestoryEventPool = systems.GetWorld().GetPool<ObstacleDestroyEvent>();

        var lifeTimePool = systems.GetWorld().GetPool<GameObjectLifeTime>();

        foreach (var obstacleDesotyrEvent in obstalceDesotoryEventFilter)
        {
            systems.GetWorld().DelEntity(obstacleDesotyrEvent);
        }

        foreach (var bullet in bulletFilter)
        {
            var bulletPosition = gameObjectPool.Get(bullet).GO.transform.position;
            foreach (var obstacle in obstacleFilter)
            {
                var obstaclePosition = gameObjectPool.Get(obstacle).GO.transform.position;
                if (Vector3.Distance(bulletPosition, obstaclePosition) < 1)
                {
                    //destory obstacle and bullet
                    lifeTimePool.Get(obstacle).deadTime = 0;
                    lifeTimePool.Get(bullet).deadTime = 0;

                    //create obstacle destory event
                    obstalceDestoryEventPool.Add(systems.GetWorld().NewEntity());

                    break; //only one obstacle by one bullet
                }
            }
        }
    }
}

public class ObstacleSpawnSystem : IEcsRunSystem
{
    private float lastSpawnTime;
    private float spawnInterval = 0.5f;

    public void Run(IEcsSystems systems)
    {
        float time = Time.time;
        if (time - lastSpawnTime > spawnInterval)
        {
            lastSpawnTime = time;

            var obstacleEntity = systems.GetWorld().NewEntity();

            var velocityPool = systems.GetWorld().GetPool<Velocity>();
            var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
            var lifeTimePool = systems.GetWorld().GetPool<GameObjectLifeTime>();
            var obstaclePool = systems.GetWorld().GetPool<ObstacleTag>();
            var destroyOnExitPool = systems.GetWorld().GetPool<DestroyOnExitTag>();

            float xPos = (Mathf.PerlinNoise1D(time) - 0.5f) * 20;
            (gameObjectPool.Add(obstacleEntity).GO = GameObject.CreatePrimitive(PrimitiveType.Cube))
                .transform.position = new Vector3(xPos, 0, 30);

            velocityPool.Add(obstacleEntity).direction = new Vector3(0, 0, -10);
            lifeTimePool.Add(obstacleEntity).deadTime = Time.time + 5;
            obstaclePool.Add(obstacleEntity);
            destroyOnExitPool.Add(obstacleEntity);
        }
    }
}


public class GameObjectRotationSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var playerFilter = systems.GetWorld().Filter<PlayerTag>().Inc<GameObjectRef>().Inc<RotateAngle>().End();
        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var rotationAnglePool = systems.GetWorld().GetPool<RotateAngle>();

        foreach (var player in playerFilter)
        {
            float angle = rotationAnglePool.Get(player).angle * Time.deltaTime * 20;
            gameObjectPool.Get(player).GO.transform.Rotate(Vector3.up, angle);
            rotationAnglePool.Del(player);
        }
    }
}

public class LookAtPlayerSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var lookAtPlayerFilter = systems.GetWorld().Filter<LookAtPlayer>().Inc<GameObjectRef>().End();
        var playerFilter = systems.GetWorld().Filter<PlayerTag>().Inc<GameObjectRef>().End();

        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        foreach (var player in playerFilter)
        {
            foreach (var follow in lookAtPlayerFilter)
            {
                gameObjectPool.Get(follow).GO.transform.LookAt(gameObjectPool.Get(player).GO.transform);
            }
        }
    }
}

public class FollowPlayerSystem : IEcsRunSystem, IEcsInitSystem
{
    public void Init(IEcsSystems systems)
    {
        var gameobjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var cameraFollowPool = systems.GetWorld().GetPool<FollowPlayer>();
        var lookAtPlayerPool = systems.GetWorld().GetPool<LookAtPlayer>();

        var mainCameraEntity = systems.GetWorld().NewEntity();
        gameobjectPool.Add(mainCameraEntity).GO = Camera.main.gameObject;
        cameraFollowPool.Add(mainCameraEntity).offset = new Vector3(0, 2, -5);
        lookAtPlayerPool.Add(mainCameraEntity);
    }

    public void Run(IEcsSystems systems)
    {
        var playerGo = systems.GetWorld().Filter<PlayerTag>().Inc<GameObjectRef>().End();
        var followGo = systems.GetWorld().Filter<FollowPlayer.Trigger>().Inc<GameObjectRef>().End();

        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var playerFollowPool = systems.GetWorld().GetPool<FollowPlayer.Trigger>();
        foreach (var player in playerGo)
        {
            foreach (var follow in followGo)
            {
                var playerTransform = gameObjectPool.Get(player).GO.transform;

                Vector3 offset = playerFollowPool.Get(follow).offset;
                Vector4 pos = playerTransform.localToWorldMatrix * new Vector4(offset.x, offset.y, offset.z, 1);

                gameObjectPool.Get(follow).GO.transform.position = pos;
            }
        }
    }
}


public class GameObjectLifeTimeSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var lifeTimeGameObjects = systems.GetWorld().Filter<GameObjectRef>().Inc<GameObjectLifeTime>().End();
        var lifeTimePool = systems.GetWorld().GetPool<GameObjectLifeTime>();
        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();

        foreach (var entity in lifeTimeGameObjects)
        {
            var deadTime = lifeTimePool.Get(entity).deadTime;
            if (deadTime < Time.time)
            {
                Object.Destroy(gameObjectPool.Get(entity).GO);
                systems.GetWorld().DelEntity(entity);
            }
        }
    }
}


public class FireSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var firePlayer = systems.GetWorld().Filter<PlayerTag>().Inc<GameObjectRef>().Inc<FireEvent>().End();
        var bulletPool = systems.GetWorld().GetPool<BulletTag>();
        var velocityPool = systems.GetWorld().GetPool<Velocity>();
        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var lifeTimePool = systems.GetWorld().GetPool<GameObjectLifeTime>();
        var destroyOnExitPool = systems.GetWorld().GetPool<DestroyOnExitTag>();

        foreach (var player in firePlayer)
        {
            var bullet = systems.GetWorld().NewEntity();
            bulletPool.Add(bullet);
            velocityPool.Add(bullet).direction = gameObjectPool.Get(player).GO.transform.forward * 50;
            var bulletGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletGo.transform.position = gameObjectPool.Get(player).GO.transform.position;
            gameObjectPool.Add(bullet).GO = bulletGo;
            lifeTimePool.Add(bullet).deadTime = Time.time + 1;
            destroyOnExitPool.Add(bullet);
        }
    }
}

public class VelocitySystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var velocityFilter = systems.GetWorld().Filter<Velocity>().Exc<MoveDirection>().End();
        var velocityPool = systems.GetWorld().GetPool<Velocity>();
        var movePool = systems.GetWorld().GetPool<MoveDirection>();
        foreach (var entity in velocityFilter)
        {
            movePool.Add(entity).direction = velocityPool.Get(entity).direction;
        }
    }
}

public class MovementSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var movable = systems.GetWorld().Filter<GameObjectRef>().Inc<MoveDirection>().End();
        var goPool = systems.GetWorld().GetPool<GameObjectRef>();
        var movePool = systems.GetWorld().GetPool<MoveDirection>();
        float dt = Time.deltaTime;
        foreach (var movabl in movable)
        {
            goPool.Get(movabl).GO.transform.Translate(movePool.Get(movabl).direction * dt);
            movePool.Del(movabl);
        }
    }
}


public class InputSystem : IEcsRunSystem, IEcsInitSystem
{
    public void Run(IEcsSystems systems)
    {
        var playersFilter = systems.GetWorld().Filter<PlayerTag>().Inc<GameObjectRef>().End();
        var followPlayerFilter = systems.GetWorld().Filter<FollowPlayer>().Inc<GameObjectRef>().End();

        var movePool = systems.GetWorld().GetPool<MoveDirection>();
        var rotateAnglePool = systems.GetWorld().GetPool<RotateAngle>();
        var moveSpeedPool = systems.GetWorld().GetPool<MoveSpeed>();
        var triggerFirePool = systems.GetWorld().GetPool<FireEvent>();
        var followTriggerPool = systems.GetWorld().GetPool<FollowPlayer.Trigger>();
        var followPlayerPool = systems.GetWorld().GetPool<FollowPlayer>();

        foreach (var fireevent in systems.GetWorld().Filter<FireEvent>().End())
        {
            triggerFirePool.Del(fireevent);
        }

        foreach (var followTrigger in systems.GetWorld().Filter<FollowPlayer.Trigger>().End())
        {
            followTriggerPool.Del(followTrigger);
        }

        foreach (var rotateAngle in systems.GetWorld().Filter<RotateAngle>().End())
        {
            rotateAnglePool.Del(rotateAngle);
        }


        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, 0);


        foreach (var player in playersFilter)
        {
            bool hasKeyInput = input is not { x: 0, z: 0 };
            bool hasMouseInput = Input.mousePositionDelta.x != 0;
            if (hasKeyInput)
            {
                float speed = 1;
                if (moveSpeedPool.Has(player))
                {
                    speed = moveSpeedPool.Get(player).speed;
                }

                movePool.Add(player).direction = input * speed;
            }

            if (hasMouseInput)
            {
                rotateAnglePool.Add(player).angle = Input.mousePositionDelta.x;
            }

            if (hasKeyInput || hasMouseInput)
            {
                foreach (var followPlayer in followPlayerFilter)
                {
                    followTriggerPool.Add(followPlayer).offset = followPlayerPool.Get(followPlayer).offset;
                }
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                triggerFirePool.Add(player);
            }
        }
    }

    public void Init(IEcsSystems systems)
    {
        var followPlayerFilter = systems.GetWorld().Filter<FollowPlayer>().End();

        var followTriggerPool = systems.GetWorld().GetPool<FollowPlayer.Trigger>();
        var followPlayerPool = systems.GetWorld().GetPool<FollowPlayer>();

        foreach (var followPlayer in followPlayerFilter)
        {
            followTriggerPool.Add(followPlayer).offset = followPlayerPool.Get(followPlayer).offset;
        }
    }
}

public class PlayerInitSystem : IEcsRunSystem, IEcsInitSystem
{
    public void Init(IEcsSystems systems)
    {
        var playersFilter = systems.GetWorld().Filter<PlayerTag>().End();
        var playersPool = systems.GetWorld().GetPool<PlayerTag>();
        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var moveSpeedPool = systems.GetWorld().GetPool<MoveSpeed>();
        var destoryOnExitPool = systems.GetWorld().GetPool<DestroyOnExitTag>();

        if (playersFilter.GetEntitiesCount() == 0)
        {
            //crate new player entity
            var newPlayerEntity = systems.GetWorld().NewEntity();
            GameObject playerGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playersPool.Add(newPlayerEntity);
            gameObjectPool.Add(newPlayerEntity).GO = playerGO;
            moveSpeedPool.Add(newPlayerEntity).speed = 10;
            destoryOnExitPool.Add(newPlayerEntity);
            playerGO.transform.position = new Vector3(systems.GetShared<SharedData>().savedPlayerXPosition, 0, 0);
            Debug.Log("Player was created!!!");
        }
    }

    public void Run(IEcsSystems systems)
    {
    }
}


public struct FollowPlayer
{
    public Vector3 offset;

    public struct Trigger
    {
        public Vector3 offset;
    }
}

public struct LookAtPlayer
{
}

public struct PlayerTag
{
}

public struct GameObjectRef
{
    public GameObject GO;
}

public struct GameObjectLifeTime
{
    public float deadTime;
}

public struct MoveDirection
{
    public Vector3 direction;
}

public struct RotateAngle
{
    public float angle;
}

public struct Velocity
{
    public Vector3 direction;
}

public struct MoveSpeed
{
    public float speed;
}

public struct FireEvent
{
}

public struct ObstacleDestroyEvent
{
}

public struct PointsUpdateEvent
{
}

public struct PlayerDeadEvent
{
}

public struct BulletTag
{
}

public struct ObstacleTag
{
}

public struct DestroyOnExitTag
{
}

public class SharedData
{
    public int points;
    public int target;
    public bool isWon;

    public float savedPlayerXPosition;

    public SharedData()
    {
    }

    public void Reset()
    {
        points = 0;
        target = 0;
        isWon = false;
    }
}

public class GameManager : MonoBehaviour
{
    EcsWorld _world;
    EcsSystems _systems;


    public void StartGame(SharedData data)
    {
        // Создаем окружение, подключаем системы.
        _world = new EcsWorld();
        _systems = new EcsSystems(_world, data);
        _systems
            .Add(new PlayerInitSystem())
            .Add(new MovementSystem())
            .Add(new VelocitySystem())
            .Add(new FireSystem())
            .Add(new GameObjectLifeTimeSystem())
            .Add(new FollowPlayerSystem())
            .Add(new LookAtPlayerSystem())
            //.Add(new GameObjectRotationSystem())
            .Add(new InputSystem())
            .Add(new ObstacleSpawnSystem())
            .Add(new BulletDestoryObstacleSystem())
            .Add(new PlayerDeadByObstacleSystem())
            .Add(new PointsEarnSystem())
            .Add(new DisplaySystem())
            .Add(new PlayerWinSystem())
            .Add(new GameObjectCleanUpSystem())
            .Init();
    }

    void Update()
    {
        // Выполняем все подключенные системы.
        _systems?.Run();
    }

    void OnDestroy()
    {
        // Уничтожаем подключенные системы.
        if (_systems != null)
        {
            _systems.Destroy();
            _systems = null;
        }

        // Очищаем окружение.
        if (_world != null)
        {
            _world.Destroy();
            _world = null;
        }
    }
}