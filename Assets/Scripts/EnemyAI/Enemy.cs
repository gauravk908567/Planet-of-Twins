using UnityEngine;

[RequireComponent(typeof(EnemyStateMachine))]
public class Enemy : MonoBehaviour, ITimeFreezable
{
    public EnemyStateMachine StateMachine { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemyDetection Detection { get; private set; }
    public EnemyAttackController AttackController { get; private set; }

    public IEnemyState IdleState { get; private set; }
    public IEnemyState ChaseState { get; private set; }
    public IEnemyState AttackState { get; private set; }

    public Transform Target { get; set; }

    [SerializeField] private float attackRange = 2f;
    public float AttackRange => attackRange;

    public FactionComponent Faction { get; private set; }

    private void Awake()
    {
        StateMachine = GetComponent<EnemyStateMachine>();
        Movement = GetComponent<EnemyMovement>();
        Detection = GetComponent<EnemyDetection>();
        AttackController = GetComponent<EnemyAttackController>();

        IdleState = new EnemyIdleState(this);
        ChaseState = new EnemyChaseState(this);
        AttackState = new EnemyAttackState(this);

        Faction = GetComponent<FactionComponent>();

    }

    private void Start()
    {
        GlobalTimeController.Instance.Register(this);
        GlobalTimeController.Instance.Register(Movement);
        StateMachine.ChangeState(IdleState);
    }

    public void OnFreeze()
    {
        StateMachine.enabled = false;
        Movement.enabled = false;
    }

    public void OnUnfreeze()
    {
        StateMachine.enabled = true;
        Movement.enabled = true;
    }

    private void OnDestroy()
    {
        if (GlobalTimeController.Instance != null)
            GlobalTimeController.Instance.Unregister(this);
    }
}