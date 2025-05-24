// AttackCommand.cs

using UnityEngine;

namespace AegisCore2D.UnitScripts
{
    public class AttackCommand : IUnitCommand
    {
        private readonly IDamageable target;
        private readonly Unit executingUnit; // Ссылка на юнита, выполняющего команду

        public AttackCommand(Unit unit, IDamageable target)
        {
            this.executingUnit = unit;
            this.target = target;
        }

        public void Execute(Unit unit) // unit здесь - это тот же самый executingUnit
        {
            if (unit != executingUnit)
            {
                Debug.LogError("AttackCommand: Выполняющий юнит не совпадает с инициализирующим юнитом!");
                return;
            }

            if (target == null || !target.IsAlive)
            {
                // Цель мертва или не существует, команда завершена (неудачно)
                //Debug.Log($"{unit.name}: Цель для AttackCommand мертва или отсутствует.");
                unit.ClearCurrentCommand(); // Сообщаем юниту, что команда выполнена (или провалена)
                return;
            }

            AttackComponent attackComp = unit.AttackComponent;
            UnitMove moveComp = unit.MoveComponent;

            if (attackComp == null)
            {
                Debug.LogError($"{unit.name} не имеет AttackComponent для выполнения AttackCommand.");
                unit.ClearCurrentCommand();
                return;
            }

            // Проверяем, может ли юнит атаковать эту цель (в радиусе)
            if (attackComp.TryAttack(target))
            {
                // Атака совершена.
                // Для простой логики "атаковал - команда выполнена"
                // Если нужна логика "продолжать атаковать, пока цель жива",
                // то команда не должна сразу завершаться.
                // Пока оставим так: одна атака = одна выполненная команда.
                // Юнит может решить перепоставить эту команду, если цель еще жива.
                // Debug.Log($"{unit.name} успешно атаковал {target.MyGameObject.name} через AttackCommand.");
                // unit.ClearCurrentCommand(); // Пока не будем очищать, чтобы юнит мог сам решить
            }
            else
            {
                // Не смогли атаковать (не в радиусе, на кулдауне и т.д.)
                // Если не в радиусе, нужно двигаться к цели.
                float distanceToTarget = Vector2.Distance(unit.transform.position, target.MyTransform.position);
                if (distanceToTarget > attackComp.GetAttackRange())
                {
                    if (moveComp != null)
                    {
                        //Debug.Log($"{unit.name} движется к {target.MyGameObject.name} для атаки.");
                        moveComp.MoveTo(target.MyTransform.position);
                        // Команда НЕ завершена, юнит будет двигаться, и в следующих Update() команда снова вызовется.
                    }
                    else
                    {
                        //Debug.LogWarning($"{unit.name} не может двигаться к цели для атаки (нет MoveComponent).");
                        //unit.ClearCurrentCommand(); // Не можем ни атаковать, ни двигаться
                    }
                }
                else
                {
                    // В радиусе, но не смогли атаковать (например, кулдаун)
                    //Debug.Log($"{unit.name} в радиусе атаки {target.MyGameObject.name}, но не может атаковать (возможно, кулдаун).");
                    // Команда НЕ завершена, ждем следующего тика Update, когда кулдаун может пройти.
                    // Если юнит просто стоит и ждет кулдауна, он должен перестать двигаться.
                    if (moveComp != null) // Не важно, двигался он или нет, если он у цели и ждет КД, он должен стоять.
                    {
                        moveComp.StopAndHoldPosition();
                    }
                }
            }
        }

        // Дополнительный метод, чтобы проверить, актуальна ли еще команда
        public bool IsTargetStillValid()
        {
            return target != null && target.IsAlive;
        }
        
        public IDamageable GetTarget()
        {
            return target;
        }
    }
}