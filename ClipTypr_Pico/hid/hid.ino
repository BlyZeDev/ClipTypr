#include "PluggableUSBHID.h"
#include "USBKeyboard.h"

typedef uint8_t u8;
typedef uint16_t u16;
typedef uint32_t u32;
typedef uint64_t u64;

typedef int8_t s8;
typedef int16_t s16;
typedef int32_t s32;
typedef int64_t s64;

USBKeyboard Keyboard;

void setup()
{
    Serial.begin(115200);
    while (!Serial) delay(10);
    Serial.println("Setup complete");
}

void loop()
{
    if (Serial.available())
    {
        Serial.println("Printing...");
        Keyboard.printf("Hello world\n");
        delay(1000);
    }
}