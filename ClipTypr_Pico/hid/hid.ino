#include "PluggableUSBHID.h"
#include "USBKeyboard.h"

const char* Token = "FF302DB2-AFCB-4EC2-93DE-87A6EABF47CC";
const size_t TokenLength = strlen(Token);
const char Separator = '|';
const unsigned long BlinkDuration = 300;

USBKeyboard Keyboard;
bool isHandlingStream;
unsigned long ledOnTime = 0;

void processChunk(String chunk)
{
    while (true)
    {
        int tokenPos = chunk.indexOf(Token);
        if (tokenPos < 0)
        {
            if (isHandlingStream)
            {
                ledOnTime = millis();
                digitalWrite(LED_BUILTIN, HIGH);

                Keyboard.printf("%s", chunk.c_str());
            }
            break;
        }

        if (tokenPos > 0 && isHandlingStream)
        {
            ledOnTime = millis();
            digitalWrite(LED_BUILTIN, HIGH);

            String beforeToken = chunk.substring(0, tokenPos);
            Keyboard.printf("%s", beforeToken.c_str());
        }

        isHandlingStream = !isHandlingStream;

        chunk = chunk.substring(tokenPos + TokenLength);
    }
}

void setup()
{
    pinMode(LED_BUILTIN, OUTPUT);
    digitalWrite(LED_BUILTIN, LOW);

    Serial.begin(115200);
    while (!Serial) delay(10);
    isHandlingStream = false;
}

void loop()
{
    if (Serial.available())
    {
        String chunk = Serial.readStringUntil(Separator);
        processChunk(chunk);
        digitalWrite(LED_BUILTIN, LOW);
    }

    if (digitalRead(LED_BUILTIN) == HIGH)
    {
        if (millis() - ledOnTime >= BlinkDuration)
        {
            digitalWrite(LED_BUILTIN, LOW);
        }
    }
}