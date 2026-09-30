# Custom melee swing

## First weapon: Hunter's Long Sword

The mod's first weapon is `LongSword`, using the vanilla Terra Blade sprite.
Craft it with 20 Wood and 10 Iron/Lead Bars at an Anvil.
Left click after each attack: OverheadSlash, Thrust, then RisingSlash.
Holding the button does not repeat attacks. RisingSlash ends the combo; after its normal recovery,
wait 30 fixed ticks (0.5 seconds) before the next click starts a new combo at OverheadSlash.
The sword stays visible in its final pose throughout this cooldown, with no damage or extra delay.
An unfinished combo resets if more than 90 ticks (1.5 seconds) pass after the previous attack.
Tune the three named attacks and their allowed continuations in `LongswordMove.cs`;
Thrust takes 12 ticks plus 3 recovery ticks before attack-speed modifiers, and has MotionValue 65.
It holds the blade at the locked aim angle and extends/retracts it with `HoldOffsetCurve`.
`WideFinisher` is retained as a move for future branches, outside the default left-click sequence.
These are the first prototype attacks for the mod's Monster Hunter-inspired weapon roster.

`BaseSwingProjectile` เป็นคลาสแม่ของ held melee projectile ใช้คู่กับ `BaseSwingItem`
หรือ spawn จาก `ModItem.Shoot` ของอาวุธเดิมก็ได้ แต่ละ projectile คือหนึ่งจังหวะของ combo
และตี NPC ตัวเดิมได้หนึ่งครั้งต่อจังหวะโดยค่าเริ่มต้น

## กำหนดว่าท่าไหนต่อจากท่าไหนได้

`Core/Combat/ComboManager.cs` จัดการ state และตรวจเส้นทาง combo
ส่วน `LongswordMove.cs` กำหนดชื่อท่า ค่าการฟัน และรายการ `NextMoves`
ของ Long Sword ปัจจุบันเชื่อมเป็น:

```text
OverheadSlash -> Thrust -> RisingSlash -> End -> 0.5s cooldown
```

รูปแบบของแต่ละท่าคือ `new ComboMove(ชื่อท่า, ค่าการฟัน, ท่าที่ต่อได้...)`:

```csharp
new ComboMove(OverheadSlash, new SwingStep
{
    StartAngleDegrees = -115f,
    EndAngleDegrees = 65f,
    DurationTicks = 26
}, Thrust, WideFinisher)
```

ตัวอย่างนี้อนุญาตให้ `OverheadSlash` ต่อไป `Thrust` หรือ `WideFinisher`
ท่าแรกในรายการคือทางเดินเริ่มต้นเมื่อไม่ระบุท่าที่ต้องการ
คลาสลูก override `RequestedMoveId` เพื่อเลือกทางแยกได้
`ComboManager.TryBegin` จะปฏิเสธการข้ามไปท่าที่ไม่มีในรายการ โดยไม่เปลี่ยน state หรือยืด timeout
เปลี่ยน ID ของท่าแล้วต้องแก้ link ให้ตรงกันด้วย; ชื่อซ้ำหรือ link ไปท่าที่ไม่มีจะตรวจพบเมื่อสร้าง combo

ใช้ `ComboManager.GetAvailableMoves` เพื่อดูท่าที่ต่อได้ และ `ComboDefinition.CanFollow`
เพื่อตรวจ link ของคู่ท่า ถ้า `NextMoves` ว่างถือว่าจบชุด เมื่อท่านั้นและ recovery จบ
จะรอ `ComboDefinition.EndCooldownTicks` (เริ่มต้น 30 ticks = 0.5 วินาที) ก่อนเริ่มใหม่ที่ `StartMoveId`
เวลานี้คงที่ ไม่หาร attack speed และไม่เกิดระหว่างท่าที่มี continuation; กำหนด 0 เพื่อไม่มี cooldown ได้
`GetAvailableMoves` จะคืนรายการว่างและ `TryBegin` คืน false ระหว่าง cooldown โดยไม่ต่อเวลาจากการกดซ้ำ
ผู้ใช้ manager แยกต่างหากต้องเรียก `CompleteMove` หลังท่าและ recovery จบจริง; parent projectile เรียกให้อัตโนมัติ
เมื่อ combo หมดเวลา ก็เริ่มที่ `StartMoveId` เช่นกัน
Manager แยกตามผู้เล่น/ชนิดอาวุธ/projectile และการเลือกท่าจริงยัง sync ผ่าน projectile

คลาส projectile ของอาวุธที่ใช้ graph override เพียง:

```csharp
protected override ComboDefinition Combo => LongswordMove.Combo;
```

อาวุธเดิมที่ override `ComboSteps` ตามตัวอย่างด้านล่างยังใช้ได้:
คลาสแม่จะแปลงรายการเป็น combo วนตามลำดับให้อัตโนมัติ

## ตัวอย่างอาวุธ combo 3 จังหวะ

สร้างสองไฟล์จากโค้ดด้านล่าง ชื่อ texture ในตัวอย่างใช้รูป Wooden Sword ของเกม
จึงทดลองได้โดยไม่ต้องมี sprite หรือ shader ใหม่

### `Content/Items/Weapons/Melee/GuildComboSword.cs`

```csharp
using GuildArsenal.Content.Projectiles.Melee;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace GuildArsenal.Content.Items.Weapons.Melee
{
    public class GuildComboSword : BaseSwingItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.WoodenSword}";
        protected override int SwingProjectileType => ModContent.ProjectileType<GuildComboSwing>();

        protected override void SetWeaponDefaults()
        {
            Item.width = Item.height = 40;
            Item.damage = 30;
            Item.knockBack = 5f;
            Item.useTime = Item.useAnimation = 24;
            Item.scale = 1.5f;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(silver: 50);
        }
    }
}
```

### `Content/Projectiles/Melee/GuildComboSwing.cs`

```csharp
using System.Collections.Generic;

namespace GuildArsenal.Content.Projectiles.Melee
{
    public class GuildComboSwing : BaseSwingProjectile
    {
        // เก็บ array ไว้ครั้งเดียว ไม่ต้องสร้างใหม่ทุก frame
        private static readonly SwingStep[] Steps =
        {
            new SwingStep
            {
                StartAngleDegrees = -110f,
                EndAngleDegrees = 90f,
                DurationTicks = 24,
                RecoveryTicks = 4,
                DamageStart = 0.25f,
                DamageEnd = 0.8f,
                ScaleCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0.65f),
                    new SwingKeyframe(0.45f, 1.15f),
                    new SwingKeyframe(1f, 0.8f))
            },
            new SwingStep
            {
                StartAngleDegrees = 95f,
                EndAngleDegrees = -110f, // กลับทิศใน combo จังหวะที่สอง
                DurationTicks = 20,
                RecoveryTicks = 4,
                ScaleCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0.8f),
                    new SwingKeyframe(0.5f, 1.25f),
                    new SwingKeyframe(1f, 0.9f))
            },
            new SwingStep
            {
                StartAngleDegrees = -150f,
                EndAngleDegrees = 150f,
                DurationTicks = 32,
                RecoveryTicks = 10,
                MotionValue = 150f,
                DamageStart = 0.3f,
                DamageEnd = 0.85f,
                // ง้างช้า แล้วเร่งฟัน; Value คือสัดส่วนระหว่างมุมเริ่มกับมุมจบ
                RotationCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0f),
                    new SwingKeyframe(0.3f, 0.05f),
                    new SwingKeyframe(0.65f, 0.95f, SwingEasing.CubicOut),
                    new SwingKeyframe(1f, 1f)),
                ScaleCurve = new SwingCurve(
                    new SwingKeyframe(0f, 0.6f),
                    new SwingKeyframe(0.55f, 1.7f),
                    new SwingKeyframe(1f, 0.75f))
            }
        };

        protected override IReadOnlyList<SwingStep> ComboSteps => Steps;
        protected override int ComboResetTicks => 90;
        protected override int TrailLength => 10;
    }
}
```

## ค่าที่ปรับได้

| ค่า | ความหมาย |
| --- | --- |
| `StartAngleDegrees` / `EndAngleDegrees` | องศา relative ต่อทิศเล็งที่ล็อกตอนเริ่มฟัน: 0 เล็งตรง, ค่าลบ/บวกกำหนดด้านของ arc และ mirror ตามทิศผู้เล่น |
| `DurationTicks` | จำนวน tick ของการฟัน ก่อนหาร attack speed; `null` ใช้ `Item.useTime` |
| `RecoveryTicks` | พักที่ท่าจบก่อนฟันครั้งถัดไป; ไม่มีดาเมจในช่วงนี้ |
| `ScaleTimingWithAttackSpeed` | `false` ให้ระยะเวลาคงที่ ไม่หาร attack speed |
| `ScaleCurve` | ขนาดตามเวลา 0..1 คูณขนาด item ที่รวม prefix/โบนัสแล้ว; hitbox และ trail ใช้ scale ของช่วงเวลาเดียวกัน |
| `RotationEasing` / `RotationCurve` | ความเร็วการหมุน; curve ที่กำหนดเองมีลำดับความสำคัญสูงกว่า easing |
| `DamageStart` / `DamageEnd` | ช่วงเวลา normalized ที่ทำดาเมจ เช่น 0.25..0.8 |
| `MotionValue` | ดาเมจท่า = weapon damage × (MotionValue / 100); ค่าเริ่มต้น 100 |
| `ReverseBladeEdge` | กลับด้านคมดาบตามแนวยาวโดยปลายดาบยังชี้ตามมุมเดิม; ใช้กับ RisingSlash |
| `TrailEdgeSharpness` | ความคม/ความบางของขอบแสง shader trail; ค่าสูงขอบยิ่งบาง |
| `TrailStartRatio` | จุดเริ่ม shader ของท่าฟันตามความยาวดาบ; ค่าเริ่มต้น 0.16 เว้นด้าม 16% และวาดส่วนที่เหลือ; ไม่เปลี่ยน Thrust/hitbox |
| `CollisionWidth` | ความหนา hitbox ก่อนคูณ scale |
| `CollisionStartRatio` | เริ่ม hitbox ที่ส่วนไหนของความยาวดาบ เช่น 0.1 เว้นด้าม 10% |
| `HoldOffset` | ระยะห่างจากมือไปยังจุดหมุนของ sprite ก่อนคูณ scale |
| `HoldOffsetCurve` | ระยะขยับตามทิศปลายดาบในแต่ละเวลา เพิ่มจาก HoldOffset; ใช้ทำการดึงดาบกลับ แทง และถอนดาบ |
| `SoundAt` | เวลา normalized ที่เล่นเสียง swing |
| `ComboResetTicks` | ถ้าไม่ฟันต่อภายในเวลานี้หลังท่าก่อนจบ ให้เริ่ม combo ที่ index 0 |

หนึ่งวินาทีเท่ากับ 60 tick ส่วน `SwingKeyframe.Time` เป็นสัดส่วนของท่าฟัน:
0 คือเริ่ม, 0.5 คือครึ่งท่า, 1 คือจบ `Easing` ของ keyframe ใช้สำหรับช่วงที่เคลื่อนเข้าหา keyframe นั้น
มุมไม่ได้ถูกบังคับให้วิ่งเส้นทางสั้นที่สุด จึงกำหนดท่าฟันเกิน 180 องศาหรือหมุนเต็มรอบได้

## ใช้กับ ModItem เดิม

ตั้ง `Item.noMelee = true`, `Item.noUseGraphic = true`, `Item.shoot = ModContent.ProjectileType<YourSwing>()`,
`Item.shootSpeed = 1f` และ `Item.UseSound = null` ตรวจ `player.ownedProjectileCounts[Item.shoot] == 0`
ใน `CanUseItem` เพื่อไม่ให้เกิด held swing ซ้อนกัน การยิง projectile แบบปกติของ item
จะส่ง velocity เป็นทิศเล็งให้คลาสแม่ล็อกมุมได้ ไม่ต้องอ่านเมาส์ใน `OnSpawn`

`CurrentMoveId` คือชื่อท่าปัจจุบัน ส่วน `ComboIndex` คือ index ของท่าใน definition สำหรับ sync
เส้นทางท่าจริงอ้างอิง `NextMoves` ไม่ใช่ตำแหน่งใน array
เปลี่ยนอาวุธกลางท่าจะยกเลิกท่าบนเครื่องเจ้าของ ไม่มีการบันทึก combo ลง save

## จุดต่อยอดของคลาสลูก

Override `EvaluateRotation(progress)`, `EvaluateScale(progress)` หรือ `EvaluateHoldOffset(progress)` เพื่อเขียนสูตรเอง
และใช้ `OnSwingStarted`, `OnSwingUpdate`, `OnSwingFinished`, `OnRecoveryUpdate`
สำหรับ particle, projectile เสริม หรือเอฟเฟกต์แต่ละ combo
ถ้า spawn projectile เพิ่มใน hook ให้ตรวจ `Projectile.owner == Main.myPlayer` ก่อน
เพื่อไม่ให้เกิดซ้ำบน server/เครื่องอื่น

รูปดาบเริ่มต้นถือว่าด้ามอยู่มุมล่างซ้ายและปลายอยู่มุมบนขวา
คลาสแม่ใช้ texture ของ item ที่เริ่มท่า หากรูปหรือจุดหมุนต่างจากนี้ให้ override
`GetDrawTexture`, `GetDrawOrigin`, `SpriteRotationOffset`, `DrawEffects` และ `BladeLength`
โดย `BladeLength` คือระยะจากจุดหมุนถึงปลายดาบ หน่วย pixel ก่อนคูณ scale

Trail เริ่มต้นเป็น sprite afterimage ที่เก็บขนาดแต่ละ frame โดยไม่ต้องมี shader
LongSword เปิด `UseShaderTrail` เพื่อวาด ribbon สีเขียวแบบ additive ด้วย `Assets/Effects/SwingTrail.fx`
ท่าฟันใช้ trail แบบกว้างตามเดิมด้วย `TrailStartRatio = 0.16f` ไม่จำกัดเฉพาะปลายดาบ
คลาสลูกอื่นเปิดได้ด้วย `protected override bool UseShaderTrail => true;`
ปรับสีด้วย `TrailColor`, ความสว่างด้วย `ShaderTrailOpacity` และความยาวด้วย `TrailLength`
mesh เก็บตำแหน่ง relative ต่อผู้เล่น พร้อมมุม/ขนาดของแต่ละ frame และแทรกจุดระหว่างมุมเร็วเพื่อลดเหลี่ยม
ตอนวาดทุก pose ใช้ตำแหน่งผู้เล่นปัจจุบันร่วมกัน ทำให้ trail เคลื่อนตามตัวโดยไม่ยืดหรือเบี้ยวจากการเดิน
ยังคง motion ของดาบ เช่น มุมฟัน ขนาด และการยืด/ถอน Thrust; ไม่เปลี่ยน hitbox หรือดาเมจ
ท่าแทงใช้ shader ribbon เส้นกลางเพียงเส้นเดียวตามแนวดาบแทน fan ซึ่งไม่มีพื้นที่เมื่อมุมไม่เปลี่ยน
หัวสามเหลี่ยมขอบตรงสีขาวอมเขียวมีพื้นที่สว่างเต็ม ไม่ใช้ลายคลื่นของท่าฟัน และไม่มีเส้นข้าง
ขยายแสงให้พ้นด้านข้างและปลาย sprite ดาบ พร้อมเก็บตำแหน่งปลายที่แทงไกลสุดจากประวัติช่วงสั้น ๆ
จึงยังเห็นรอยแทงตอนถอนดาบโดย mesh ไม่พับทับตัวเอง; แสงเริ่มช่วงออกแรงและจางระหว่างถอน
shader ทำขอบแสงขาวอมเขียวและไล่จางไปยังหาง; ไม่โหลด asset บน dedicated server
override `DrawSwingTrail`/`PostDrawSwing` เมื่อต้องการเอฟเฟกต์อื่น
ใช้ `ConfigureProjectileDefaults`/`ConfigureStaticDefaults` แทน override `SetDefaults`/`SetStaticDefaults`
และใช้ `WriteSwingExtraAI`/`ReadSwingExtraAI` ถ้าคลาสลูกมีค่าที่ต้อง sync เพิ่ม

## Motion value และ RisingSlash

กำหนด `MotionValue` ใน `LongswordMove.cs` โดยตรง: OverheadSlash = 100, Thrust = 65,
RisingSlash = 105 และ WideFinisher = 135 (ท่าหลังยังไม่อยู่ใน combo คลิกซ้าย)
ค่านี้เป็นค่าตั้งต้นที่รักษาสัดส่วนดาเมจ prototype เดิม ไม่ใช่ตาราง MV ทางการของ Monster Hunter
สูตร `weapon damage * (MotionValue / 100f)` ใช้กับ source damage ครั้งเดียวทั้ง NPC และ PvP
weapon damage ในที่นี้คือ damage ที่ item ส่งให้ projectile หลังรวมโบนัสของผู้เล่น
ไม่แก้ `Item.damage` และไม่คูณ `Projectile.damage` ซ้ำ; เกราะ, damage variance และ crit ยังผ่านระบบเกม
ตัวอย่าง damage 38: MV 100 = 38, MV 65 = 24.7, MV 105 = 39.9 ก่อนระบบเกมปัดเศษ/คิดเกราะ/crit

RisingSlash ใช้ 20 ticks + recovery 4 ตามเดิม, มุม +75 ถึง -115 องศา,
แล้วพักท้าย combo อีก 30 ticks คงที่ (0.5 วินาที) ก่อนเริ่ม combo ใหม่
ดาบและแขนค้างมุม/ขนาด/ระยะของ frame สุดท้ายจนจบ cooldown นี้ โดยยังเคลื่อนตามตัวผู้เล่น
held projectile อยู่ต่อระหว่าง cooldown แต่ไม่มีดาเมจ และไม่เริ่ม cooldown ซ้ำเมื่อ projectile หาย
`ComboRecoveryDuration` / `IsInComboRecovery` แยกช่วงพักท้ายชุดจาก recovery ปกติ
ใช้ `OnComboRecoveryUpdate(progress)` สำหรับเอฟเฟกต์ช่วงพักท้ายชุด โดย progress อยู่ใน 0..1
ง้างถึงเวลา 0.22 แล้วเร่งฟันขึ้นถึง 93% ของ arc ที่เวลา 0.52
กลับด้านคมดาบด้วย `ReverseBladeEdge = true` โดยไม่เปลี่ยนทิศปลายดาบ/hitbox
และใช้ `TrailEdgeSharpness = 110` ให้ขอบ trail บางกว่าท่าอื่น

## คอมไพล์ shader

เมื่อแก้ `.fx` ให้เรียกจากโฟลเดอร์มอด:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/CompileShaders.ps1
dotnet build GuildArsenal.csproj --no-restore
```

script ใช้ EasyXnb ที่มีอยู่ใน `Complier` ผ่านสำเนาใน `obj/ShaderCompiler`
ไม่แก้ config ต้นฉบับ และตรวจว่าได้ XNB ใหม่จริงก่อนรายงานว่าสำเร็จ
`Assets/Effects/SwingTrail.xnb` คือ asset ที่โหลดจริงและต้องอยู่ในแพ็กมอด
ดูรูปแบบ pipeline ได้จาก [EasyXnb source](https://github.com/SuperAndyHero/EasyXnb)
และ [tModLoader shader guide](https://github.com/tModLoader/tModLoader/wiki/Expert-Shader-Guide)
