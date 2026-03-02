using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Leopotam.EcsLite;
using Newtonsoft.Json;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

//система сохранения и загрузки, модель в ecs синкается с юнити
//система асинхронной загрузки ассетов, моделей для игрока и патронов и препятсвий

[Serializable]
public struct SVector3
{
    public float x;
    public float y;
    public float z;

    public SVector3(float vector3X, float vector3Y, float vector3Z)
    {
        x = vector3X;
        y = vector3Y;
        z = vector3Z;
    }

    public static implicit operator Vector3(SVector3 vector3)
    {
        return new Vector3(vector3.x, vector3.y, vector3.z);
    }

    public static implicit operator SVector3(Vector3 vector3)
    {
        return new SVector3(vector3.x, vector3.y, vector3.z);
    }
}


public class SaveSystem : IEcsInitSystem, IEcsDestroySystem
{
    string bulletPositionsKey = "bulletPositions";
    string bulletVelocityKey = "bulletVelocity";

    public void Init(IEcsSystems systems)
    {
        Debug.Log("SaveSystem: Init - Начинаем восстановление пуль");
        Restore(systems);
        Debug.Log("SaveSystem: Init - Восстановление завершено");
    }

    public void Destroy(IEcsSystems systems)
    {
        Debug.Log("SaveSystem: Destroy - Начинаем сохранение пуль");
        Save(systems);
        Debug.Log("SaveSystem: Destroy - Сохранение завершено");
    }

    public void Restore(IEcsSystems systems)
    {
        if (PlayerPrefs.HasKey(bulletPositionsKey) == false)
        {
            Debug.Log("SaveSystem: Restore - Ключи не найдены, пропускаем восстановление");
            return;
        }

        string positionsJson = PlayerPrefs.GetString(bulletPositionsKey);
        string velocityJson = PlayerPrefs.GetString(bulletVelocityKey);
        
        List<SVector3> bulletPositions;
        List<SVector3> bulletVelocity;

        try
        {
            bulletPositions = JsonConvert.DeserializeObject<List<SVector3>>(positionsJson);
            bulletVelocity = JsonConvert.DeserializeObject<List<SVector3>>(velocityJson);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveSystem: Restore - Ошибка десериализации JSON: {e.Message}");
            return;
        }

        if (bulletPositions == null || bulletVelocity == null || bulletPositions.Count != bulletVelocity.Count)
        {
            Debug.LogError("SaveSystem: Restore - Несоответствие данных или null списки");
            return;
        }

        var positionPool = systems.GetWorld().GetPool<Position>();
        var velocityPool = systems.GetWorld().GetPool<Velocity>();
        var bulletTagPool = systems.GetWorld().GetPool<BulletTag>();


        for (int i = 0; i < bulletPositions.Count; i++)
        {
            try
            {
                var newEntity = systems.GetWorld().NewEntity();
                positionPool.Add(newEntity).position = bulletPositions[i];
                velocityPool.Add(newEntity).direction = bulletVelocity[i];
                bulletTagPool.Add(newEntity);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveSystem: Restore - Ошибка создания пули {i}: {e.Message}");
            }
        }

    }

    public void Save(IEcsSystems systems)
    {
        List<SVector3> bulletPositions = new();
        List<SVector3> bulletVelocity = new();

        var bulletFilter = systems.GetWorld().Filter<BulletTag>().Inc<Position>().Inc<Velocity>().End();

        var positionPool = systems.GetWorld().GetPool<Position>();
        var velocityPool = systems.GetWorld().GetPool<Velocity>();

        int index = 0;
        foreach (var bullet in bulletFilter)
        {
            try
            {
                var position = positionPool.Get(bullet).position;
                var velocity = velocityPool.Get(bullet).direction;

                bulletPositions.Add(position);
                bulletVelocity.Add(velocity);

                index++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveSystem: Save - Ошибка получения данных пули {bullet}: {e.Message}");
            }
        }
        
        try
        {
            string positionsJson = JsonConvert.SerializeObject(bulletPositions);
            string velocityJson = JsonConvert.SerializeObject(bulletVelocity);
            
            PlayerPrefs.SetString(bulletPositionsKey, positionsJson);
            PlayerPrefs.SetString(bulletVelocityKey, velocityJson);
            PlayerPrefs.Save(); // Принудительное сохранение

        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveSystem: Save - Ошибка сериализации/сохранения: {e.Message}");
        }
    }
}


public class SyncPositionWithUnitySystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var toSyncFilter = systems.GetWorld().Filter<GameObjectRef>().Inc<Position>().End();

        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var positionPool = systems.GetWorld().GetPool<Position>();

        foreach (var tySync in toSyncFilter)
        {
            var go = gameObjectPool.Get(tySync).GO;
            var position = positionPool.Get(tySync).position;
            go.transform.position = position;
        }
    }
}


public class GameObjectCleanUpSystem : IEcsPostDestroySystem
{
    public void PostDestroy(IEcsSystems systems)
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
            foreach (var player in systems.GetWorld().Filter<PlayerTag>().Inc<Position>().End())
            {
                var playerPos = systems.GetWorld().GetPool<Position>().Get(player).position;
                shared.savedPlayerXPosition = playerPos.x;
            }
        }
    }
}


public class DisplaySystem : IEcsRunSystem, IEcsInitSystem
{
    public void Run(IEcsSystems systems)
    {
        var pointsUpdateEventFilter = systems.GetWorld().Filter<PointsUpdateEvent>().End();

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
        var playerFiler = systems.GetWorld().Filter<PlayerTag>().Inc<Position>().End();
        var obstacleFilter = systems.GetWorld().Filter<ObstacleTag>().Inc<Position>().End();
        var lifeTimeFilter = systems.GetWorld().Filter<GameObjectLifeTime>().End();

        var positionPool = systems.GetWorld().GetPool<Position>();
        var lifeTimePool = systems.GetWorld().GetPool<GameObjectLifeTime>();

        var playerDeadEventFIlter = systems.GetWorld().Filter<PlayerDeadEvent>().End();
        var playerDeadEventPool = systems.GetWorld().GetPool<PlayerDeadEvent>();
        foreach (var playerDead in playerDeadEventFIlter)
        {
            systems.GetWorld().DelEntity(playerDead);
        }

        foreach (var player in playerFiler)
        {
            var playerPosition = positionPool.Get(player).position;
            foreach (var obstacle in obstacleFilter)
            {
                var obstaclePosition = positionPool.Get(obstacle).position;
                if (Vector3.SqrMagnitude(playerPosition - obstaclePosition) < 1)
                {
                    //dead

                    //remove all lifre time objects
                    foreach (var lifeTime in lifeTimeFilter)
                    {
                        lifeTimePool.Get(lifeTime).deadTime = 0;
                    }

//points = 0
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
        var bulletFilter = systems.GetWorld().Filter<BulletTag>().Inc<Position>().End();
        var obstacleFilter = systems.GetWorld().Filter<ObstacleTag>().Inc<Position>().End();
        var obstalceDesotoryEventFilter = systems.GetWorld().Filter<ObstacleDestroyEvent>().End();

        var positionPool = systems.GetWorld().GetPool<Position>();
        var obstalceDestoryEventPool = systems.GetWorld().GetPool<ObstacleDestroyEvent>();

        var lifeTimePool = systems.GetWorld().GetPool<GameObjectLifeTime>();

        foreach (var obstacleDesotyrEvent in obstalceDesotoryEventFilter)
        {
            systems.GetWorld().DelEntity(obstacleDesotyrEvent);
        }

        foreach (var bullet in bulletFilter)
        {
            var bulletPosition = positionPool.Get(bullet).position;
            foreach (var obstacle in obstacleFilter)
            {
                var obstaclePosition = positionPool.Get(obstacle).position;
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
            var positionPool = systems.GetWorld().GetPool<Position>();

            float xPos = (Mathf.PerlinNoise1D(time) - 0.5f) * 20;
            gameObjectPool.Add(obstacleEntity).GO = GameObject.CreatePrimitive(PrimitiveType.Cube);

            velocityPool.Add(obstacleEntity).direction = new Vector3(0, 0, -10);
            lifeTimePool.Add(obstacleEntity).deadTime = Time.time + 5;
            obstaclePool.Add(obstacleEntity);
            destroyOnExitPool.Add(obstacleEntity);
            positionPool.Add(obstacleEntity).position = new Vector3(xPos, 0, 30);
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
        var positionPool = systems.GetWorld().GetPool<Position>();

        var mainCameraEntity = systems.GetWorld().NewEntity();
        gameobjectPool.Add(mainCameraEntity).GO = Camera.main.gameObject;
        cameraFollowPool.Add(mainCameraEntity).offset = new Vector3(0, 2, -5);
        lookAtPlayerPool.Add(mainCameraEntity);
        positionPool.Add(mainCameraEntity);
    }

    public void Run(IEcsSystems systems)
    {
        var playerGo = systems.GetWorld().Filter<PlayerTag>().Inc<GameObjectRef>().End();
        var followGo = systems.GetWorld().Filter<FollowPlayer.Trigger>().Inc<Position>().End();

        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var positionPool = systems.GetWorld().GetPool<Position>();
        var playerFollowPool = systems.GetWorld().GetPool<FollowPlayer.Trigger>();
        foreach (var player in playerGo)
        {
            foreach (var follow in followGo)
            {
                var playerLocalToWorldMatrix = gameObjectPool.Get(player).GO.transform.localToWorldMatrix;

                Vector3 offset = playerFollowPool.Get(follow).offset;
                Vector4 pos = playerLocalToWorldMatrix * new Vector4(offset.x, offset.y, offset.z, 1);

                positionPool.Get(follow).position = pos;
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

public class BulletInitSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var toInitFilter = systems.GetWorld().Filter<BulletTag>().Inc<Position>().Inc<Velocity>().Exc<GameObjectRef>()
            .End();

        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();
        var lifeTimePool = systems.GetWorld().GetPool<GameObjectLifeTime>();
        var destroyOnExitPool = systems.GetWorld().GetPool<DestroyOnExitTag>();

        foreach (var toInit in toInitFilter)
        {
            gameObjectPool.Add(toInit).GO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lifeTimePool.Add(toInit).deadTime = Time.time + 10;
            destroyOnExitPool.Add(toInit);
        }
    }
}


public class FireSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var firePlayer = systems.GetWorld().Filter<PlayerTag>().Inc<Position>().Inc<FireEvent>().End();
        var bulletPool = systems.GetWorld().GetPool<BulletTag>();
        var velocityPool = systems.GetWorld().GetPool<Velocity>();
        var positionPool = systems.GetWorld().GetPool<Position>();
        var gameObjectPool = systems.GetWorld().GetPool<GameObjectRef>();

        foreach (var player in firePlayer)
        {
            var bullet = systems.GetWorld().NewEntity();
            bulletPool.Add(bullet);
            velocityPool.Add(bullet).direction = gameObjectPool.Get(player).GO.transform.forward * 10;
            positionPool.Add(bullet).position = positionPool.Get(player).position;
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
        var movable = systems.GetWorld().Filter<Position>().Inc<MoveDirection>().End();
        var positionPool = systems.GetWorld().GetPool<Position>();
        var movePool = systems.GetWorld().GetPool<MoveDirection>();
        float dt = Time.deltaTime;
        foreach (var movabl in movable)
        {
            positionPool.Get(movabl).position += movePool.Get(movabl).direction * dt;
            movePool.Del(movabl);
        }
    }
}


public class InputSystem : IEcsRunSystem, IEcsInitSystem
{
    public void Run(IEcsSystems systems)
    {
        var playersFilter = systems.GetWorld().Filter<PlayerTag>().Inc<Position>().End();
        var followPlayerFilter = systems.GetWorld().Filter<FollowPlayer>().Inc<Position>().End();

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
        var positionPool = systems.GetWorld().GetPool<Position>();
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
            positionPool.Add(newPlayerEntity).position =
                new Vector3(systems.GetShared<SharedData>().savedPlayerXPosition, 0, 0);
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

public struct Position
{
    public Vector3 position;
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
            .Add(new SyncPositionWithUnitySystem())
            .Add(new SaveSystem())
            .Add(new BulletInitSystem())
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