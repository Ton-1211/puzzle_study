using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

interface IState
{
    public enum E_State
    {
        Control = 0,
        GameOver = 1,
        Falling = 2,

        Max,

        Unchanged,
    }

    E_State Initialize(PlayDirector parent);
    E_State Update(PlayDirector parent);
}
[RequireComponent(typeof(BoardController))]
public class PlayDirector : MonoBehaviour
{
    [SerializeField] GameObject player = default!;
    PlayerController _playerController = null;
    LogicalInput _logicalInput = new();
    BoardController _boardController = default!;
    NextQueue _nextQueue = new();
    [SerializeField] PuyoPair[] nextPuyoPairs = { default!, default! };// ��next�̃Q�[���I�u�W�F�N�g�̐���
    // ��ԊǗ�
        IState.E_State _current_state = IState.E_State.Falling;
        static readonly IState[] states = new IState[(int)IState.E_State.Max] {
            new ControlState(),
            new GameOverState(),
            new FallingState(),
        };
    // Start is called before the first frame update
    void Start()
    {
        _playerController = player.GetComponent<PlayerController>();
        _boardController = GetComponent<BoardController>();
        _logicalInput.Clear();
        _playerController.SetLogicalInput(_logicalInput);
        _nextQueue.Initialize();
        // ��Ԃ̏�����
        InitializeState();
    }

    void UpdateNextsView()
    {
        _nextQueue.Each((int idx, Vector2Int n) =>
        {
            nextPuyoPairs[idx++].SetPuyoType((PuyoType)n.x, (PuyoType)n.y);
        });
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // ���͂���荞��
        UpdateInput();

        UpdateState();

        if (!player.activeSelf)
        {
            Spawn(_nextQueue.Update());
        }
    }

    bool Spawn(Vector2Int next) => _playerController.Spawn((PuyoType)next[0], (PuyoType)next[1]);

    static readonly KeyCode[] key_code_tbl = new KeyCode[(int)LogicalInput.Key.Max]
{
        KeyCode.RightArrow, // Right
        KeyCode.LeftArrow,  // Left
        KeyCode.X,          // RotR
        KeyCode.Z,          // RotL
        KeyCode.UpArrow,    // QuickDrop
        KeyCode.DownArrow,  // Down
};

    // ���͂���荞��
    void UpdateInput()
    {
        LogicalInput.Key inputDev = 0;//�f�o�C�X�l

        // �L�[���͎擾
        for (int i = 0; i < (int)LogicalInput.Key.Max; i++)
        {
            if (Input.GetKey(key_code_tbl[i]))
            {
                inputDev |= (LogicalInput.Key)(1 << i);
            }
        }

        _logicalInput.Update(inputDev);
    }
    class ControlState : IState
    {
        public IState.E_State Initialize(PlayDirector parent)
        {
            if (!parent.Spawn(parent._nextQueue.Update()))
            {
                return IState.E_State.GameOver;
            }

            parent.UpdateNextsView();
            return IState.E_State.Unchanged;
        }
        public IState.E_State Update(PlayDirector parent)
        {
            return parent.player.activeSelf ? IState.E_State.Unchanged : IState.E_State.Falling;
        }
    }

    class FallingState : IState
    {
        public IState.E_State Initialize(PlayDirector parent)
        {
            return parent._boardController.CheckFall() ? IState.E_State.Unchanged : IState.E_State.Control;
        }
        public IState.E_State Update(PlayDirector parent)
        {
            return parent._boardController.Fall() ? IState.E_State.Unchanged : IState.E_State.Control;
        }
    }

    class GameOverState : IState
    {
        public IState.E_State Initialize(PlayDirector parent)
        {
            SceneManager.LoadScene(0);// ���g���C
            return IState.E_State.Unchanged;
        }
        public IState.E_State Update(PlayDirector parent) { return IState.E_State.Unchanged; }
    }

    void InitializeState()
    {
        Debug.Assert(condition: _current_state is >= 0 and IState.E_State.Max);

        var next_state = states[(int)_current_state].Initialize(this);
        if(next_state != IState.E_State.Unchanged)
        {
            _current_state = next_state;
            InitializeState();// �������ŏ�Ԃ��ς��悤�Ȃ�A�ċA�I�ɏ������Ăяo��
        }
    }

    void UpdateState()
    {
        Debug.Assert(condition: _current_state is >= 0 and < IState.E_State.Max);

        var next_state = states[(int)_current_state].Update(this);
        if( next_state != IState.E_State.Unchanged)
        {
            // ���̏�Ԃ֑J��
            _current_state = next_state;
            InitializeState();
        }
    }
}

