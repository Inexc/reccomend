import asyncio, edge_tts
text = "Оно знает, что ты хочешь посмотреть."
async def main():
    c = edge_tts.Communicate(text, "ru-RU-DmitryNeural", rate="+10%", pitch="-2Hz")
    await c.save(r"C:\Users\egiba\OneDrive\Рабочий стол\ТРПО\программа\MovieBookRecommenderCS\Реклама\audio\test.mp3")
asyncio.run(main())
print("done")
