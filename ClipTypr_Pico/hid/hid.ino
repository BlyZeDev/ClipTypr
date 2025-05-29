#include "USBKeyboard.h"
#include "PluggableUSBHID.h"

const size_t BufferSize = 4096;

USBKeyboard Keyboard;
char buffer[BufferSize];
size_t bytesRead;

void setup()
{
    pinMode(LED_BUILTIN, OUTPUT);
    digitalWrite(LED_BUILTIN, HIGH);

    Serial.setTimeout(450);
    Serial.begin(115200);
    while (!Serial) delay(10);
    Keyboard.wait_ready();

    digitalWrite(LED_BUILTIN, LOW);
}

void loop()
{
    if (Serial.available())
    {
        digitalWrite(LED_BUILTIN, HIGH);

        bytesRead = Serial.readBytes(buffer, BufferSize);
        Keyboard.write(buffer, bytesRead);

        digitalWrite(LED_BUILTIN, LOW);
    }
}