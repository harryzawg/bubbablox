export const isTouchDevice = () => {
  // From https://stackoverflow.com/questions/4817029/whats-the-best-way-to-detect-a-touch-screen-device-using-javascript
  return (('ontouchstart' in window) ||
    (navigator.maxTouchPoints > 0) ||
    // @ts-ignore
    (navigator.msMaxTouchPoints > 0));
}

export const Random = (min, max) => {
  return Math.floor(Math.random() * (max - min) ) + min;
}

/**
 * @param {number} seconds
 * @returns {Promise}
 */
export const wait = (seconds) =>
    new Promise(resolve => setTimeout(resolve, seconds * 1000));

export function IsNullOrEmpty(value) {
  return !value || value.trim().length === 0;
}

export class Stopwatch {
  startTime = 0;
  endTime = 0;
  
  Start() {
    this.startTime = Date.now();
  }
  
  Stop() {
    this.endTime = Date.now();
    return this.endTime - this.startTime;
  }
  
  ElapsedMilliseconds() {
    return this.endTime - this.startTime;
  }
  
  ElapsedSeconds() {
    return (this.endTime - this.startTime) / 1000;
  }
  
  ElapsedMinutes() {
    return (this.endTime - this.startTime) / 1000 * 60;
  }
  
  ElapsedHours() {
    return (this.endTime - this.startTime) / 1000 * 60 * 60;
  }
}

/**
 * @template T
 * @typedef {[T, import('react').Dispatch<T>]} UseState
 */
