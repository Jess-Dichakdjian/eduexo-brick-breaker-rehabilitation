#include <Servo.h>

// =======================================================
// EDUEXO BRICK BREAKER FINAL VERSION
// Transparent control + Low/High Assist
// USB Serial version for Unity
// =======================================================
//
// Unity sends:
// assistLevel,targetAngle
//
// Examples:
// 0,50   transparent mode
// 1,70   low assist toward target 70
// 2,30   high assist toward target 30
//
// Arduino sends:
// currentAngle,targetAngle,assistLevel,residualTorquePercent,servoCommand,rawAngle,forceRaw
//
// Unity PaddleScript reads the first 5 values:
// currentAngle,targetAngle,assistLevel,residualTorquePercent,servoCommand
//
// Extra values rawAngle and forceRaw are only for debugging.
// =======================================================

Servo myservo;

// ---------------------- PINS ----------------------
const int servoOnPin = 7;
const int PWMPin = 5;

const int angleSensorPin = A1;
const int forceAnalogInPin = A2;

const int ampliSwitchPin = 8;
const int gainSelectPin = 4;

const int buttonPin1 = 3; // Power down
const int buttonPin2 = 2; // Power up

// ---------------------- CALIBRATION ----------------------
// Your real measured values:
int posSensorMin = 850;   // arm down -> currentAngle 0
int posSensorMax = 405;   // arm up   -> currentAngle 100

int forceOffset = 499;    // forceRaw at rest

// ---------------------- GENERAL STATE ----------------------
int targetAngle = 50;
int assistanceLevel = 0;  // 0 transparent, 1 low assist, 2 high assist

byte currentAngle = 50;
byte previousTargetAngle = 50;

int rawAngle = 0;
int forceRaw = 0;

float forceIs = 0.0;
float residualTorquePercent = 0.0;

int servoCommand = 90;

// activeControlMode:
// -1 = uninitialized
//  0 = transparent admittance control
//  1 = assist control
int activeControlMode = -1;

// ---------------------- TRANSPARENT CONTROL SETTINGS ----------------------
// This part comes from the guideline/book admittance logic.

float transparentPositionFloat = 45.0;
int transparentPosition = 45;

int transparentMinCommand = 0;
int transparentMaxCommand = 110;

float transparentForceDesired = 0.0;
int transparentForceThreshold = 20;

// Start here.
// If transparent mode still feels stiff, try 0.05.
// If it shakes/runs away, try 0.01.
float transparentGain = 0.03;

// If transparent mode pushes/fights in the wrong direction, change this to -1.
int transparentForceSign = 1;

// ---------------------- ASSIST SETTINGS ----------------------
// This part follows your old working assist logic.

float assistPositionDesiredFloat = 50.0;
int assistPositionDesired = 50;

int minAngle = 5;
int maxAngle = 110;

bool targetReached = false;
int targetDeadband = 10;

// Old working gains:
// 0 = transparent
// 1 = low assist
// 2 = high assist
const float admittanceGains[3] = {
  0.0,
  0.01,
  0.05
};

// Old assist threshold was 150.
// If assist does not activate easily enough, lower to 100 or 80.
int assistForceThreshold = 150;

float assistMaxDelta = 5.0;

// Old working behavior:
// 90 = neutral
// >90 = push one direction
// <90 = push the other direction
//
// If low/high assist pushes the wrong way, change this to -1.
int assistDirectionSign = 1;

bool movingTowardsTarget = false;

// ---------------------- TELEMETRY ----------------------
unsigned long lastTelemetryTime = 0;
const unsigned long telemetryIntervalMs = 50; // 20 Hz

// =======================================================
// SETUP
// =======================================================

void setup() {
  Serial.begin(115200);
  delay(1000);

  pinMode(buttonPin1, INPUT_PULLUP);
  pinMode(buttonPin2, INPUT_PULLUP);

  attachInterrupt(digitalPinToInterrupt(buttonPin1), PowerDown, FALLING);
  attachInterrupt(digitalPinToInterrupt(buttonPin2), PowerUp, FALLING);

  pinMode(gainSelectPin, OUTPUT);
  digitalWrite(gainSelectPin, LOW);

  pinMode(ampliSwitchPin, OUTPUT);
  digitalWrite(ampliSwitchPin, HIGH);

  pinMode(servoOnPin, OUTPUT);

  // IMPORTANT:
  // HIGH is needed so the angle sensor works.
  // We discovered A1 only gives correct position when servoOnPin is HIGH.
  digitalWrite(servoOnPin, HIGH);

  rawAngle = analogRead(angleSensorPin);
  currentAngle = mapRawAngleToPercent(rawAngle);

  forceRaw = analogRead(forceAnalogInPin);
  forceIs = (float)(forceRaw - forceOffset);

  initTransparentFromCurrentAngle();

  myservo.attach(PWMPin);
  myservo.write(transparentPosition);

  // Minimal startup text.
  // Unity will ignore this because it is not numeric CSV.
  Serial.println("EduExo final controller started");
  Serial.println("currentAngle,targetAngle,assistLevel,residualTorquePercent,servoCommand,rawAngle,forceRaw");
}

// =======================================================
// LOOP
// =======================================================

void loop() {
  readSerialCommandFromUnity();

  rawAngle = analogRead(angleSensorPin);
  forceRaw = analogRead(forceAnalogInPin);

  currentAngle = mapRawAngleToPercent(rawAngle);

  forceIs = (float)(forceRaw - forceOffset);

  // Residual torque proxy for logging.
  // 5.0 means 500 raw-force difference becomes 100%.
  residualTorquePercent = constrain(abs(forceIs) / 5.0, 0.0, 100.0);

  updateController();

  sendTelemetry();

  delay(5);
}

// =======================================================
// ANGLE MAPPING
// =======================================================

byte mapRawAngleToPercent(int raw) {
  float t = 0.0;

  if (posSensorMin != posSensorMax) {
    t = (raw - posSensorMin) / (float)(posSensorMax - posSensorMin);
  }

  t = constrain(t, 0.0, 1.0);
  return (byte)round(t * 100.0);
}

float mapCurrentAngleToTransparentCommand() {
  float t = currentAngle / 100.0;
  t = constrain(t, 0.0, 1.0);

  float cmd = transparentMinCommand +
              t * (transparentMaxCommand - transparentMinCommand);

  return constrain(cmd, transparentMinCommand, transparentMaxCommand);
}

// =======================================================
// UNITY SERIAL COMMAND READER
// =======================================================

void readSerialCommandFromUnity() {
  if (!Serial.available()) {
    return;
  }

  String line = Serial.readStringUntil('\n');
  line.trim();

  if (line.length() == 0) {
    return;
  }

  int commaIndex = line.indexOf(',');

  if (commaIndex >= 0) {
    String assistPart = line.substring(0, commaIndex);
    String targetPart = line.substring(commaIndex + 1);

    int newAssistLevel = assistPart.toInt();
    int newTargetAngle = targetPart.toInt();

    newAssistLevel = constrain(newAssistLevel, 0, 2);
    newTargetAngle = constrain(newTargetAngle, 0, 100);

    assistanceLevel = newAssistLevel;

    if (newTargetAngle != previousTargetAngle) {
      targetAngle = newTargetAngle;
      previousTargetAngle = (byte)newTargetAngle;

      targetReached = false;

      // Start assist from current measured position to avoid jumps.
      assistPositionDesiredFloat = (float)currentAngle;
      assistPositionDesired = currentAngle;

      if (assistanceLevel > 0) {
        activeControlMode = -1; // force re-init assist mode
      }
    }
  } else {
    // Single number command: assist level only
    int newAssistLevel = line.toInt();
    assistanceLevel = constrain(newAssistLevel, 0, 2);
  }
}

// =======================================================
// MAIN CONTROLLER
// =======================================================

void updateController() {
  if (assistanceLevel == 0) {
    enterTransparentIfNeeded();
    updateTransparentControl();
    return;
  }

  // If we are close enough to the target, use transparent mode.
  if (!targetReached && abs(targetAngle - currentAngle) <= targetDeadband) {
    targetReached = true;
  }

  if (targetReached) {
    enterTransparentIfNeeded();
    updateTransparentControl();
    return;
  }

  // If user is not applying enough force, stay in transparent behavior.
  // This avoids the motor holding/stiffening unnecessarily.
  if (abs(forceIs) <= assistForceThreshold) {
    enterTransparentIfNeeded();
    updateTransparentControl();
    return;
  }

  enterAssistIfNeeded();
  updateAssistControl();
}

// =======================================================
// TRANSPARENT CONTROL
// =======================================================

void enterTransparentIfNeeded() {
  if (activeControlMode != 0) {
    initTransparentFromCurrentAngle();
    activeControlMode = 0;
  }

  if (!myservo.attached()) {
    myservo.attach(PWMPin);
  }
}

void initTransparentFromCurrentAngle() {
  transparentPositionFloat = mapCurrentAngleToTransparentCommand();
  transparentPositionFloat = constrain(
    transparentPositionFloat,
    transparentMinCommand,
    transparentMaxCommand
  );

  transparentPosition = round(transparentPositionFloat);
  servoCommand = transparentPosition;
}

void updateTransparentControl() {
  float forceError = forceIs - transparentForceDesired;

  if (abs(forceError) > transparentForceThreshold) {
    transparentPositionFloat +=
      transparentForceSign * transparentGain * forceError;
  }

  transparentPositionFloat = constrain(
    transparentPositionFloat,
    transparentMinCommand,
    transparentMaxCommand
  );

  transparentPosition = round(transparentPositionFloat);
  servoCommand = constrain(transparentPosition, 0, 180);

  myservo.write(servoCommand);
}

// =======================================================
// LOW / HIGH ASSIST CONTROL
// =======================================================

void enterAssistIfNeeded() {
  if (activeControlMode != 1) {
    assistPositionDesiredFloat = (float)currentAngle;
    assistPositionDesired = currentAngle;
    activeControlMode = 1;
  }

  if (!myservo.attached()) {
    myservo.attach(PWMPin);
  }
}

void updateAssistControl() {
  float admittanceGain = admittanceGains[constrain(assistanceLevel, 0, 2)];

  // Prevent desired position from lagging behind the current arm angle.
  if (targetAngle > currentAngle && assistPositionDesiredFloat < currentAngle) {
    assistPositionDesiredFloat = (float)currentAngle;
  } else if (targetAngle < currentAngle && assistPositionDesiredFloat > currentAngle) {
    assistPositionDesiredFloat = (float)currentAngle;
  }

  float previousPosition = assistPositionDesiredFloat;

  float delta = admittanceGain * forceIs;
  delta = constrain(delta, -assistMaxDelta, assistMaxDelta);

  float newPositionDesired = assistPositionDesiredFloat + delta;
  newPositionDesired = constrain(newPositionDesired, (float)minAngle, (float)maxAngle);

  movingTowardsTarget =
    abs(newPositionDesired - targetAngle) < abs(previousPosition - targetAngle);

  if (movingTowardsTarget) {
    assistPositionDesiredFloat = newPositionDesired;

    // Avoid overshoot.
    if (previousPosition < targetAngle && assistPositionDesiredFloat > targetAngle) {
      assistPositionDesiredFloat = targetAngle;
    } else if (previousPosition > targetAngle && assistPositionDesiredFloat < targetAngle) {
      assistPositionDesiredFloat = targetAngle;
    }

    assistPositionDesiredFloat = constrain(
      assistPositionDesiredFloat,
      (float)minAngle,
      (float)maxAngle
    );

    assistPositionDesired = round(assistPositionDesiredFloat);

    // Your old working motor command style.
    servoCommand = 90 + assistDirectionSign * (int)(delta * 18.0);
    servoCommand = constrain(servoCommand, 0, 180);

    myservo.write(servoCommand);
  } else {
    // If calculated movement would not bring user closer,
    // fall back to transparent admittance instead of detaching.
    enterTransparentIfNeeded();
    updateTransparentControl();
  }
}

// =======================================================
// TELEMETRY TO UNITY
// =======================================================

void sendTelemetry() {
  unsigned long now = millis();

  if (now - lastTelemetryTime < telemetryIntervalMs) {
    return;
  }

  lastTelemetryTime = now;

  Serial.print(currentAngle);
  Serial.print(",");
  Serial.print(targetAngle);
  Serial.print(",");
  Serial.print(assistanceLevel);
  Serial.print(",");
  Serial.print(residualTorquePercent, 2);
  Serial.print(",");
  Serial.print(servoCommand);
  Serial.print(",");
  Serial.print(rawAngle);
  Serial.print(",");
  Serial.println(forceRaw);
}

// =======================================================
// SAFETY BUTTONS
// =======================================================

void PowerUp() {
  digitalWrite(servoOnPin, HIGH);

  if (!myservo.attached()) {
    myservo.attach(PWMPin);
  }
}

void PowerDown() {
  if (myservo.attached()) {
    myservo.detach();
  }

  digitalWrite(servoOnPin, LOW);
}