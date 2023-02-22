console.log('hello');


fetch('/api/locations').then(async cities => {
    console.log(await cities.json());
});