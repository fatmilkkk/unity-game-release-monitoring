# Unity Portfolio Game — «Шар-собиратель»

Небольшой 3D-проект для портфолио: игрок управляет шаром, собирает монеты в кошелёк,
активирует бонусы, уворачивается от подвижных препятствий и старается не врезаться
на скорости в бочки со взрывчаткой.

**Движок:** Unity 2022.3 LTS (Built-in Render Pipeline)
**Пакеты:** Cinemachine 2.10, Unity UI

## Как открыть

1. Склонировать репозиторий.
2. В Unity Hub выбрать **Add → Add project from disk** и указать папку проекта.
   Открыть в Unity 2022.3.x (подойдёт любая 2022.3 LTS).
3. При первом открытии сцена **`Assets/Scenes/Main.unity` собирается автоматически**
   скриптом `Assets/Editor/DemoSceneBuilder.cs`. Вместе со сценой создаются материалы, префабы,
   анимации и контроллер анимации бонуса.
   Пересобрать сцену вручную можно через меню **Tools → Portfolio Demo → Build Demo Scene**.
4. Нажать **Play**.

## Управление

| Клавиша | Действие |
|---|---|
| WASD / стрелки | Движение (относительно камеры) |
| Пробел | Прыжок |

## Что реализовано

### 1. Слежение камерой — Cinemachine Virtual Camera
- На `Main Camera` стоит `CinemachineBrain`.
- Объект `CM vcam Player` (`CinemachineVirtualCamera`): Follow и LookAt указывают на игрока.
- Body — **Transposer** в режиме *World Space* со смещением (0, 7, −9) и демпфированием,
  поэтому камера не крутится вместе с катящимся шаром.
- Aim — **Composer** с dead zone / soft zone и упреждением (lookahead), поэтому слежение плавное.
- Настройка: `DemoSceneBuilder.SetupCameras()`.

### 2. Анимация бонуса — машина состояний Animator
- `Assets/Animations/BonusAnimator.controller` содержит bool-параметр `PlayerInZone` и 4 состояния:
  - **Idle** — медленное вращение и покачивание (цикл);
  - **Activate** — игрок вошёл в зону: бонус подпрыгивает, увеличивается и вспыхивает;
  - **Active** — игрок в зоне: быстрое вращение и пульсация (цикл);
  - **Deactivate** — игрок вышел: бонус возвращается в исходное состояние.
- Переходы: Idle→Activate и Deactivate→Activate (`PlayerInZone = true`),
  Activate→Active (по Exit Time), Active→Deactivate и Activate→Deactivate (`PlayerInZone = false`),
  Deactivate→Idle (по Exit Time).
- Код: `Assets/Scripts/Bonus/BonusZone.cs`. Сферический триггер ловит вход и выход игрока
  и переключает параметр в Animator.

### 3. Монеты и кошелёк
- Префаб `Assets/Prefabs/Coin.prefab` (`Coin.cs`): монета вращается и покачивается.
  При касании игрока добавляется в кошелёк, проигрывается эффект частиц, монета исчезает.
- `Wallet.cs` хранит баланс (с сохранением в `PlayerPrefs`), умеет `AddCoins` и `TrySpend`
  и рассылает событие `CoinsChanged`.
- `WalletUI.cs` показывает баланс на экране с анимацией-пульсацией.
- Обнулить кошелёк: контекстное меню компонента Wallet → «Обнулить кошелёк».

### 4. Интерактивные препятствия
- **Бочка со взрывчаткой** (`ExplosiveBarrel.cs`, префаб `ExplosiveBarrel.prefab`).
  Если игрок врезается в неё со скоростью **не меньше 7 м/с** (`collision.relativeVelocity`),
  бочка взрывается и игрок уничтожается. Слабый удар просто толкает бочку.
  Взрыв отбрасывает физические объекты (`AddExplosionForce`), ранит игрока рядом
  и вызывает цепную реакцию соседних бочек.
- **Подвижные препятствия** (`MovingObstacle.cs`): блоки ездят туда-обратно, балка вращается.
  При касании наносят урон и отбрасывают игрока.
- **Здоровье игрока** (`PlayerHealth.cs`, `HealthUI.cs`): 100 HP, неуязвимость после удара,
  красная вспышка. При низком HP полоса здоровья краснеет и мигает. При 0 HP игрок уничтожается,
  и уровень перезапускается.

### 5. Частицы перемещения игрока
`PlayerMovementParticles.cs` управляет тремя системами частиц:
- **Dust** — пыль из-под шара. Чем выше скорость, тем больше пыли; появляется только на земле.
- **SpeedTrail** — светящийся след на скорости, опасной для бочек (≥ 7 м/с).
- **LandingBurst** — облачко пыли при приземлении после прыжка или падения.

Ещё есть эффекты сбора монеты, взрыва (огонь, дым, искры, вспышка света) и гибели игрока.

## Структура

```
Assets/
  Editor/DemoSceneBuilder.cs     — сборка сцены, префабов, анимаций, частиц
  Scripts/
    Player/      PlayerController, PlayerHealth
    Collectables/Coin, Wallet
    Bonus/       BonusZone
    Obstacles/   ExplosiveBarrel, MovingObstacle
    Effects/     PlayerMovementParticles, AutoDestroy
    UI/          WalletUI, HealthUI
```
