from findhim.config import get_pipeline
from findhim.tools.locations import fetch_power_plants

if __name__ == "__main__":
    pipeline = get_pipeline()

    df = pipeline.fetch_people()

    filtered = pipeline.filter_people(df)
    pipeline.display_people(filtered, label="Po filtracji (M, 20-40 lat, Grudziądz)")

    tag_results = pipeline.tag_jobs(filtered)
    pipeline.print_tag_stats(filtered, tag_results)

    transport_people = pipeline.select_by_tag(filtered, tag_results, tag="transport")

    print(f"\n--- Osoby z tagiem 'transport' ({len(transport_people)}) ---\n")
    for person in transport_people:
        print(f"  {person['name']} {person['surname']} (ur. {person['born']}, {person['city']}): {person['tags']}")

    pipeline.save_result(transport_people)

    power_plants = fetch_power_plants()
    print(f"\n--- Elektrownie ({len(power_plants)}) ---\n")
    for plant in power_plants:
        status = "aktywna" if plant["is_active"] else "nieaktywna"
        print(f"  {plant['city']:30s} kod: {plant['code']}   moc: {plant['power']:10s} [{status}]")
