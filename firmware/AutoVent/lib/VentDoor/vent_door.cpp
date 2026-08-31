#include "vent_door.h"
#include "config.h"
#include <AccelStepper.h>

namespace vent_door
{
  static AccelStepper _stepper1(AccelStepper::FULL4WIRE, DOOR_M1_IN1_PIN, DOOR_M1_IN3_PIN, DOOR_M1_IN2_PIN, DOOR_M1_IN4_PIN);
  static AccelStepper _stepper2(AccelStepper::FULL4WIRE, DOOR_M2_IN1_PIN, DOOR_M2_IN3_PIN, DOOR_M2_IN2_PIN, DOOR_M2_IN4_PIN);
  static VentState _state = VentState::Unknown;

  static void Engage(float maxSpeed, float acceleration)
  {
    _stepper1.enableOutputs();
    _stepper2.enableOutputs();
    _stepper1.setMaxSpeed(maxSpeed);
    _stepper1.setAcceleration(acceleration);
    _stepper2.setMaxSpeed(maxSpeed);
    _stepper2.setAcceleration(acceleration);
  }

  static void Release()
  {
    _stepper1.disableOutputs();
    _stepper2.disableOutputs();
  }

  static void MoveUp(long steps)
  {
    _stepper1.moveTo(-steps);
    _stepper2.moveTo(steps);
  }

  static void MoveDown(long steps)
  {
    _stepper1.moveTo(steps);
    _stepper2.moveTo(-steps);
  }

  static bool IsAtClosedStop()
  {
    return digitalRead(DOOR_LIMIT_SWITCH_PIN) == LOW;
  }

  void Begin()
  {
    pinMode(DOOR_LIMIT_SWITCH_PIN, INPUT_PULLUP);
    Release();
  }

  bool Close()
  {
    if (_state == VentState::Closed)
    {
      return true;
    }

    _state = VentState::Closing;
    Engage(DOOR_CLOSE_MAX_SPEED, DOOR_CLOSE_ACCELERATION);
    MoveDown(DOOR_MAX_CLOSE_STEPS);

    while (_stepper1.distanceToGo() != 0 || _stepper2.distanceToGo() != 0)
    {
      _stepper1.run();
      _stepper2.run();

      if (!IsAtClosedStop())
      {
        continue;
      }

      Release();
      _stepper1.setCurrentPosition(0);
      _stepper2.setCurrentPosition(0);
      _state = VentState::Closed;

      DEBUG_PRINT("Door closed and calibrated");
      return true;
    }

    Release();
    _state = VentState::Unknown;

    DEBUG_PRINT("Failsafe reached, the door never touched the limit switch");
    return false;
  }

  bool Open()
  {
    if (_state == VentState::Open)
    {
      return true;
    }

    if (_state == VentState::Unknown && !Close())
    {
      return false;
    }

    _state = VentState::Opening;
    Engage(DOOR_OPEN_MAX_SPEED, DOOR_OPEN_ACCELERATION);
    MoveUp(DOOR_OPEN_STEPS);

    while (_stepper1.distanceToGo() != 0 || _stepper2.distanceToGo() != 0)
    {
      _stepper1.run();
      _stepper2.run();
    }

    Release();
    _state = VentState::Open;

    DEBUG_PRINT("Door opened");
    return true;
  }

  VentState State()
  {
    return _state;
  }
}
