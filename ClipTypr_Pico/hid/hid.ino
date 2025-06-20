#include "USBKeyboard.h"
#include "PluggableUSBHID.h"

const size_t BufferSize = 512;

USBKeyboard Keyboard;
char buffer[BufferSize];
size_t bytesRead;

void setup()
{
    pinMode(LED_BUILTIN, OUTPUT);

    Serial.setTimeout(50);
    Serial.begin(115200);
    while (!Serial) delay(10);

    Keyboard.wait_ready();
}

void loop()
{
    if (Serial.available())
    {
        digitalWrite(LED_BUILTIN, HIGH);

        bytesRead = Serial.readBytes(buffer, BufferSize);
        Keyboard.write(buffer, bytesRead);

        digitalWrite(LED_BUILTIN, LOW);

        delay(1);
    }
}