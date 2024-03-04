using GLU.SteeringBehaviors;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Steering))]
public class NPC_Behavior : MonoBehaviour
{
    public enum StateOfNPC
    {
        SeekGenerator,
        RepairGenerator,
        FleePlayer,
        SeekLockedAI,
        FreeLockedAI,
        BeingCarried,
        MoveToCage,
        SeekGate,
    }

    public StateOfNPC State;

    public bool FreeNPC;

    [SerializeField] private GameObject _Player;
    [SerializeField] private GameObject _Gate;
    [SerializeField] private GameObject[] _Generator;

    [SerializeField] private Cages _cages;

    private Steering _steering;
    private List<IBehavior> _behaviors;
    private GameObject _cageTarget;
    private GameObject _randomGenerator;
    private GameObject _randomCage;
    private CapsuleCollider _capsuleCollider;
    private int _repairDistance = 1;
    private int _runDistance = 10;
    private int _escapedDistance = 15;
    private bool _chooseGenerator;
    private bool _chooseCage;

    // Animation Related Components
    private Animator _Animator;

    void Start()
    {
        _Animator = GetComponent<Animator>();
        _steering = GetComponent<Steering>();
        _behaviors = new List<IBehavior>();
        State = StateOfNPC.SeekGenerator;
        _capsuleCollider = GetComponent<CapsuleCollider>();
    }

    void Update()
    {
        switch (State) 
        {
            case StateOfNPC.SeekGenerator:
                
                //clear the list of all behaviors
                _behaviors.Clear();
                AvoidLayout();
                _Animator.SetBool("IsWalking", true);
                _capsuleCollider.isTrigger = false;
                FreeNPC = false;

                //if there has not already been chosen a generator, choose a random one from the array
                if (_chooseGenerator == false)
                {
                    _randomGenerator = _Generator[Random.Range(0, _Generator.Length)];
                    _chooseGenerator = true;
                }

                //add this randomly chosen generator to the behavior from the NPC
                _behaviors.Add(new Seek(_randomGenerator));

                //when in repair distance from the generator, start repairing
                if (Vector3.Distance(transform.position, _randomGenerator.transform.position) < _repairDistance + 3)
                {
                    State = StateOfNPC.RepairGenerator;
                }

                //when all generators are done, state is now seekgate
                if (_randomGenerator.GetComponentInParent<GeneratorManager>().AllGeneratorsDone)
                {
                    State = StateOfNPC.SeekGate;
                }

                RunAway();
                break;

            case StateOfNPC.RepairGenerator:
                _behaviors.Clear();
                _behaviors.Add(new Idle());
                _Animator.SetBool("IsWalking", false);

                if (_randomGenerator.GetComponent<Generator>().GeneratorIsDone)
                {
                    //if a NPC got locked up, go free him. otherwise go look for an other generator
                    if (_cages._LockedCages.Count != 0)
                    {
                        _chooseCage = false;
                        State = StateOfNPC.SeekLockedAI;
                    }
                    else
                    {
                        _chooseGenerator = false;
                        State = StateOfNPC.SeekGenerator;
                    }
                }

                RunAway();
                break;

            case StateOfNPC.FleePlayer:
                _behaviors.Clear();
                AvoidLayout();
                _behaviors.Add(new Evade(_Player));
                _Animator.SetBool("IsWalking", true);

                //when a NPC isnt being followd by the player anymore
                if (Vector3.Distance(transform.position, _Player.transform.position) > _escapedDistance)
                {
                    if (_cages._LockedCages.Count != 0)
                    {
                        _chooseCage = false;
                        State = StateOfNPC.SeekLockedAI;
                    }
                    else
                    {
                        _chooseGenerator = false;
                        State = StateOfNPC.SeekGenerator;
                    }
                }
                break;

            case StateOfNPC.SeekLockedAI:
                _behaviors.Clear();

                _Animator.SetBool("IsWalking", true);

                AvoidLayout();

                //if there has not already been chosen a cage, choose a random one from the array
                if (_chooseCage == false)
                {
                    _randomCage = _cages._LockedCages.ToArray()[Random.Range(0, _cages._LockedCages.Count)];
                    _chooseCage = true;
                }

                //add this randomly chosen cage to the behavior from the NPC
                _behaviors.Add(new Seek(_randomCage));

                //when in repair (freeing) distance from the cage, start repairing (freeing the NPC)
                if (Vector3.Distance(transform.position, _randomCage.transform.position) < _repairDistance + 3)
                {
                    State = StateOfNPC.FreeLockedAI;
                }

                RunAway();
                break;

            case StateOfNPC.FreeLockedAI:
                _behaviors.Clear();
                _behaviors.Add(new Idle());

                //when cage is open look for a generator
                if (_randomCage.GetComponent<CageBehavior>().CageIsOpen)
                {
                    _chooseGenerator = false;
                    State = StateOfNPC.SeekGenerator;
                }
                
                //run when player gets too close
                if (Vector3.Distance(transform.position, _Player.transform.position) < _runDistance)
                {
                    this.gameObject.GetComponent<CapsuleCollider>().isTrigger = false;
                    State = StateOfNPC.FleePlayer;
                }

                break;

            case StateOfNPC.BeingCarried:
                _behaviors.Clear();
                
                //go idle
                _behaviors.Add(new Idle());

                break;

            case StateOfNPC.MoveToCage:
                _behaviors.Clear();
                
                //go to the cage the player put you in
                _behaviors.Add(new Seek(_cageTarget));

                if (FreeNPC == true)
                {
                    //remove the current cage, because no ai is locked in it anymore
                    _cages._LockedCages.Remove(_cageTarget);

                    //if a NPC got locked up, go free him. otherwise go look for an other generator
                    if (_cages._LockedCages.Count != 0)
                    {
                        _chooseCage = false;
                        State = StateOfNPC.SeekLockedAI;
                    }
                    else
                    {
                        _chooseGenerator = false;
                        State = StateOfNPC.SeekGenerator;
                    }
                }

                //when in repair (freeing) distance, go idle
                if (Vector3.Distance(transform.position, _cageTarget.transform.position) < _repairDistance)
                {
                    _behaviors.Clear();
                    _behaviors.Add(new Idle());
                }

                break;

            case StateOfNPC.SeekGate:
                _behaviors.Clear();
                AvoidLayout();
                
                //Go to the gate to get out
                _behaviors.Add(new Seek(_Gate));

                RunAway();
                break;
        }

        //Debug.Log(State);
    }

    private void OnTriggerEnter(Collider other)
    {
        //if its the NPC that got picked up, do the following only when the cage is open
        if (other.gameObject.CompareTag("EmptyCage") && State == StateOfNPC.MoveToCage)
        {
            if (other.GetComponent<CageBehavior>().CageIsOpen)
            {
                FreeNPC = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        //shoving over a wooden pallet when you walk next to this pallet
        if (other.gameObject.CompareTag("Shovable"))
        {
            Shoving_Objects shoving = other.gameObject.GetComponentInParent<Shoving_Objects>();
            shoving.HasTriggerd = true;
        }
    }

    /// <summary>
    /// Avoid all obstacles and walls, put all thes in the behaviors list
    /// </summary>
    private void AvoidLayout()
    {
        _behaviors.Add(new AvoidObstacle());
        _behaviors.Add(new AvoidWall());
        _steering.SetBehaviors(_behaviors);
    }

    /// <summary>
    /// Evade the player when it gets to closed to a NPC
    /// </summary>
    private void RunAway()
    {
        if (Vector3.Distance(transform.position, _Player.transform.position) < _runDistance)
        {
            State = StateOfNPC.FleePlayer;
        }
    }

    /// <summary>
    /// Get the cage target information
    /// </summary>
    public void SetCageTarget(GameObject _cage)
    {
        _cageTarget = _cage;
    }
}
